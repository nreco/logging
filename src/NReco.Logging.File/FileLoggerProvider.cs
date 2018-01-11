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

		public LogLevel MinLevel { get; set; } = LogLevel.Debug;

		/// <summary>
		/// Custom formatter for log entry. 
		/// </summary>
		public Func<LogMessage,string> FormatLogEntry { get; set; }

		public FileLoggerProvider(string fileName) : this(fileName, true) {
		}

		public FileLoggerProvider(string fileName, bool append) : this(fileName, new FileLoggerOptions() { Append = append } ) {
		}

		public FileLoggerProvider(string fileName, FileLoggerOptions options) {
			LogFileName = fileName;
			Append = options.Append;
			FileSizeLimitBytes = options.FileSizeLimitBytes;
			MaxRollingFiles = options.MaxRollingFiles;
			FormatLogEntry = options.FormatLogEntry;

			fWriter = new FileWriter(this);
			processQueueTask = Task.Factory.StartNew(
				ProcessQueue,
				this,
				TaskCreationOptions.LongRunning);
		}

		public ILogger CreateLogger(string categoryName) {
			return loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
		}

		public void Dispose() {
			entryQueue.CompleteAdding();
			try {
				processQueueTask.Wait(1500);  // the same as in ConsoleLogger
			} catch (TaskCanceledException) { } catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

			loggers.Clear();
			fWriter.Close();
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

		internal class FileWriter {

			readonly FileLoggerProvider FileLogPrv;
			string LogFileName;
			Stream LogFileStream;
			TextWriter LogFileWriter;

			internal FileWriter(FileLoggerProvider fileLogPrv) {
				FileLogPrv = fileLogPrv;

				DetermineLastFileLogName();
				OpenFile(FileLogPrv.Append);
			}

			void DetermineLastFileLogName() {
				if (FileLogPrv.FileSizeLimitBytes>0) {
					// rolling file is used
					var logFileMask = Path.GetFileNameWithoutExtension(FileLogPrv.LogFileName) + "*" + Path.GetExtension(FileLogPrv.LogFileName);
					var logDirName = Path.GetDirectoryName(FileLogPrv.LogFileName);
					if (String.IsNullOrEmpty(logDirName))
						logDirName = Directory.GetCurrentDirectory();
					var logFiles = Directory.GetFiles(logDirName, logFileMask, SearchOption.TopDirectoryOnly);
					if (logFiles.Length>0) {
						var lastFileInfo = logFiles
								.Select(fName => new FileInfo(fName))
								.OrderByDescending(fInfo => fInfo.Name)
								.OrderByDescending(fInfo => fInfo.LastWriteTime).First();
						LogFileName = lastFileInfo.FullName;
					} else {
						// no files yet, use default name
						LogFileName = FileLogPrv.LogFileName;
					}
				} else {
					LogFileName = FileLogPrv.LogFileName;
				}
			}

			void OpenFile(bool append) {
				var fileInfo = new FileInfo(LogFileName);
				LogFileStream = new FileStream(LogFileName, FileMode.OpenOrCreate, FileAccess.Write);
				if (append) {
					LogFileStream.Seek(0, SeekOrigin.End);
				} else {
					LogFileStream.SetLength(0); // clear the file
				}
				LogFileWriter = new StreamWriter(LogFileStream);
			}

			string GetNextFileLogName() {
				int currentFileIndex = 0;
				var baseFileNameOnly = Path.GetFileNameWithoutExtension(FileLogPrv.LogFileName);
				var currentFileNameOnly = Path.GetFileNameWithoutExtension(LogFileName);

				var suffix = currentFileNameOnly.Substring(baseFileNameOnly.Length);
				if (suffix.Length>0 && Int32.TryParse(suffix, out var parsedIndex)) {
					currentFileIndex = parsedIndex;
				}
				var nextFileIndex = currentFileIndex + 1;
				if (FileLogPrv.MaxRollingFiles > 0) {
					nextFileIndex %= FileLogPrv.MaxRollingFiles;
				}

				var nextFileName = baseFileNameOnly + (nextFileIndex>0 ? nextFileIndex.ToString() : "") + Path.GetExtension(FileLogPrv.LogFileName);
				return Path.Combine(Path.GetDirectoryName(FileLogPrv.LogFileName), nextFileName );
			}

			void CheckForNewLogFile() {
				bool openNewFile = false;
				if (FileLogPrv.FileSizeLimitBytes > 0 && LogFileStream.Length > FileLogPrv.FileSizeLimitBytes)
					openNewFile = true;

				if (openNewFile) {
					Close();
					LogFileName = GetNextFileLogName();
					OpenFile(false);
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

			internal void Close() {
				if (LogFileWriter!=null) {
					var logWriter = LogFileWriter;
					LogFileWriter = null;

					logWriter.Dispose();
					LogFileStream.Dispose();
					LogFileStream = null;
				}

			}
		}

	}

}
