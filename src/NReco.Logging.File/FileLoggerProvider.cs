#region License
/*
 * NReco file logging provider (https://github.com/nreco/logging)
 * Copyright 2017 Vitaliy Fedorchenko
 * Distributed under the MIT license
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace NReco.Logging.File {

	/// <summary>
	/// Generic file logger provider.
	/// </summary>
#if NETSTANDARD2
	[ProviderAlias("File")]
#endif
	public class FileLoggerProvider : ILoggerProvider {

		private readonly string LogFileName;

		private readonly ConcurrentDictionary<string, FileLogger> loggers =
			new ConcurrentDictionary<string, FileLogger>();
		private readonly BlockingCollection<string> entryQueue = new BlockingCollection<string>(1024);
		private readonly Task processQueueTask;
		private readonly FileWriter fWriter;

		private readonly bool Append = true;
		private readonly long FileSizeLimitBytes = 0;
		private readonly int MaxRollingFiles = 0;
		private readonly bool SkipErroneousLogFiles = true;

		public LogLevel MinLevel { get; set; } = LogLevel.Trace;

		/// <summary>
		/// Custom formatter for log entry. 
		/// </summary>
		public Func<LogMessage,string> FormatLogEntry { get; set; }

		/// <summary>
		/// Custom formatter for the log file name.
		/// </summary>
		public Func<string, string> FormatLogFileName { get; set; }

		public FileLoggerProvider(string fileName) : this(fileName, true) {
		}

		public FileLoggerProvider(string fileName, bool append) : this(fileName, new FileLoggerOptions() { Append = append } ) {
		}

		public FileLoggerProvider(string fileName, FileLoggerOptions options) {
			LogFileName = Environment.ExpandEnvironmentVariables(fileName);
			Append = options.Append;
			FileSizeLimitBytes = options.FileSizeLimitBytes;
			MaxRollingFiles = options.MaxRollingFiles;
			FormatLogEntry = options.FormatLogEntry;
			FormatLogFileName = options.FormatLogFileName;
			MinLevel = options.MinLevel;
			SkipErroneousLogFiles = options.SkipErroneousLogFiles;

			fWriter = new FileWriter(this);
			processQueueTask = Task.Factory.StartNew(
				ProcessQueue,
				this,
				TaskCreationOptions.LongRunning);
		}

		public ILogger CreateLogger(string categoryName) {
			return loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
		}

		/// <summary>
		/// Disposes this instance
		/// </summary>
		public void Dispose() {
			try {
				entryQueue.CompleteAdding();
				try {
					processQueueTask.Wait(1500);  // the same as in ConsoleLogger
				}
				catch (TaskCanceledException) { }
				catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

				loggers.Clear();
			}
			finally {
				// Finally block to ensure the file is released in case of an exception
				fWriter.Dispose();
			}
		}

		private FileLogger CreateLoggerImplementation(string name) {
			return new FileLogger(name, this);
		}

		internal void WriteEntry(string message) {
			if (!entryQueue.IsAddingCompleted) {
				try {
					entryQueue.Add(message);
					return;
				} catch (InvalidOperationException) { }
			}
			// do nothing
		}
		private void ProcessQueue() {
			foreach (var message in entryQueue.GetConsumingEnumerable()) {
				fWriter.WriteMessage(message, entryQueue.Count==0);
			}
		}

		private static void ProcessQueue(object state) {
			var fileLogger = (FileLoggerProvider)state;
			fileLogger.ProcessQueue();
		}

		internal class FileWriter : IDisposable {

			readonly FileLoggerProvider FileLogPrv;
			string LogFileName;
			Stream LogFileStream;
			TextWriter LogFileWriter;

			internal FileWriter(FileLoggerProvider fileLogPrv)
			{
				FileLogPrv = fileLogPrv;

				DetermineLastFileLogName();
				OpenNextAvailableFile(FileLogPrv.Append, true);
			}
			

			string GetBaseLogFileName() {
				var fName = FileLogPrv.LogFileName;
				if (FileLogPrv.FormatLogFileName != null)
					fName = FileLogPrv.FormatLogFileName(fName);
				return fName;
			}

			void DetermineLastFileLogName() {
				var baseLogFileName = GetBaseLogFileName();
				__LastBaseLogFileName = baseLogFileName;
				if (FileLogPrv.FileSizeLimitBytes>0) {
					// rolling file is used
					var logFileMask = Path.GetFileNameWithoutExtension(baseLogFileName) + "*" + Path.GetExtension(baseLogFileName);
					var logDirName = Path.GetDirectoryName(baseLogFileName);
					if (String.IsNullOrEmpty(logDirName))
						logDirName = Directory.GetCurrentDirectory();
					var logFiles = Directory.Exists(logDirName) ? Directory.GetFiles(logDirName, logFileMask, SearchOption.TopDirectoryOnly) : Array.Empty<string>();
					if (logFiles.Length>0) {
						var lastFileInfo = logFiles
								.Select(fName => new FileInfo(fName))
								.OrderByDescending(fInfo => fInfo.Name)
								.OrderByDescending(fInfo => fInfo.LastWriteTime).First();
						LogFileName = lastFileInfo.FullName;
					} else {
						// no files yet, use default name
						LogFileName = baseLogFileName;
					}
				} else {
					LogFileName = baseLogFileName;
				}
			}

			/// <summary>
			/// Opens the next file. If SkipErroneousLogFiles is on, it will open the next file which an IO exception does not occur.
			/// </summary>
			/// <param name="append"></param>
			void OpenNextAvailableFile(bool append, bool tryExistingFilenameFirst)
			{
				// Flush and close what we have open
				// This is necessary as GetNextFileLogName reads the file size
				// as written to disk
				Close();

				HashSet<string> attempted = new HashSet<string>();
				List<IOException> exceptions = null;

				bool rotateFilename = !tryExistingFilenameFirst;
				while (true)
				{
					if (rotateFilename)
					{
						LogFileName = GetNextFileLogName();
					}

					if (!attempted.Add(LogFileName))
					{
						// We have tried this name before
						// We have now tried all possible file names
						throw new AggregateException("All log files returned IO exceptions", exceptions);
					}

					if (TryOpenFile(append, out IOException e))
					{
						return;
					}

					// Failed to open the file.
					// Move onto the next file, if possible.
					(exceptions ?? (exceptions = new List<IOException>())).Add(e);


					rotateFilename = true;
				}
			}

			/// <summary>
			/// Tries to open a file. Catches IOExceptions if SkipErroneousLogFiles is enabled
			/// </summary>
			/// <param name="append"></param>
			/// <param name="exception"></param>
			/// <returns></returns>
			bool TryOpenFile(bool append, out IOException exception)
            {
				try
				{
					OpenFile(append);
					exception = null;
					return true;
				}
				catch (IOException e) when (FileLogPrv.SkipErroneousLogFiles)
				{
					// It's possible that the file is in use by another process,
					// temporary IO problems or or permissions issues have arisen
					exception = e;
					return false;
				}
			}

			/// <summary>
			/// Closes any open file and opens the file at LogFileName
			/// </summary>
			/// <param name="append"></param>
			void OpenFile(bool append) {
				// Close anything already open
				Close();
				
				var fileInfo = new FileInfo(LogFileName);
				
				// Directory.Create will check if the directory already exists,
				// so there is no need for a "manual" check first.
				fileInfo.Directory.Create();

				LogFileStream = new FileStream(LogFileName, FileMode.OpenOrCreate, FileAccess.Write);
				if (append) {
					LogFileStream.Seek(0, SeekOrigin.End);
				} else {
					LogFileStream.SetLength(0); // clear the file
				}
				LogFileWriter = new StreamWriter(LogFileStream);
			}

			string GetNextFileLogName() {
				var baseLogFileName = GetBaseLogFileName();
				
				int currentFileIndex = 0;
				var baseFileNameOnly = Path.GetFileNameWithoutExtension(baseLogFileName);
				var currentFileNameOnly = Path.GetFileNameWithoutExtension(LogFileName);

				var suffix = currentFileNameOnly.Substring(baseFileNameOnly.Length);
				if (suffix.Length>0 && Int32.TryParse(suffix, out var parsedIndex)) {
					currentFileIndex = parsedIndex;
				}
				var nextFileIndex = currentFileIndex + 1;
				if (FileLogPrv.MaxRollingFiles > 0) {
					nextFileIndex %= FileLogPrv.MaxRollingFiles;
				}

				var nextFileName = baseFileNameOnly + (nextFileIndex>0 ? nextFileIndex.ToString() : "") + Path.GetExtension(baseLogFileName);
				return Path.Combine(Path.GetDirectoryName(baseLogFileName), nextFileName );
			}

			// cache last returned base log file name to avoid excessive checks in CheckForNewLogFile.isBaseFileNameChanged
			string __LastBaseLogFileName = null;

			void CheckForNewLogFile() {
				if (isMaxFileSizeThresholdReached() || isBaseFileNameChanged()) {
					// We need to use a new log file
					OpenNextAvailableFile(false, false);
				}

				bool isMaxFileSizeThresholdReached() {
					return FileLogPrv.FileSizeLimitBytes > 0 && LogFileStream.Length > FileLogPrv.FileSizeLimitBytes;
				}
				bool isBaseFileNameChanged() {
					if (FileLogPrv.FormatLogFileName!=null) {
						var baseLogFileName = GetBaseLogFileName();
						if (baseLogFileName != __LastBaseLogFileName) {
							__LastBaseLogFileName = baseLogFileName;
							return true;
						}
					}
					return false;
				}
			}

			internal void WriteMessage(string message, bool flush) {
				if (LogFileWriter != null) {
					CheckForNewLogFile();
					LogFileWriter.WriteLine(message);
					if (flush)
						LogFileWriter.Flush();
				}
			}

			/// <summary>
			/// Closes and disposes of all internal streams
			/// </summary>
			void Close()
			{
				try	{
					LogFileWriter?.Dispose();
					LogFileWriter = null;
				}
				finally	{
					// use finally block to ensure release of resources in case of exception
					LogFileStream?.Dispose();
					LogFileStream = null;
				}
			}

			public void Dispose() => Close();
		}

	}

}
