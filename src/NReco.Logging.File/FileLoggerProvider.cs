﻿#region License
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
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace NReco.Logging.File
{

    /// <summary>
    /// Generic file logger provider.
    /// </summary>
    [ProviderAlias("File")]
	public class FileLoggerProvider : ILoggerProvider {

		private string LogFileName;

		private readonly ConcurrentDictionary<string, FileLogger> loggers =
			new ConcurrentDictionary<string, FileLogger>();
		private readonly BlockingCollection<string> entryQueue = new BlockingCollection<string>(1024);
		private readonly Task processQueueTask;
		private readonly FileWriter fWriter;

		internal FileLoggerOptions Options { get; private set; }

		private bool Append => Options.Append;
		private long FileSizeLimitBytes => Options.FileSizeLimitBytes;
		private int MaxRollingFiles => Options.MaxRollingFiles;

		public LogLevel MinLevel {
			get => Options.MinLevel; 
			set { Options.MinLevel = value; }
		}

		/// <summary>
		///  Gets or sets indication whether or not UTC timezone should be used to for timestamps in logging messages. Defaults to false.
		/// </summary>
		public bool UseUtcTimestamp { 
			get => Options.UseUtcTimestamp; 
			set { Options.UseUtcTimestamp = value; } 
		}

		/// <summary>
		/// Custom formatter for log entry. 
		/// </summary>
		public Func<LogMessage,string> FormatLogEntry {
			get => Options.FormatLogEntry; 
			set { Options.FormatLogEntry = value; }
		}

		/// <summary>
		/// Custom formatter for the log file name.
		/// </summary>
		public Func<string, string> FormatLogFileName {
			get => Options.FormatLogFileName; 
			set { Options.FormatLogFileName = value; }
		}

		/// <summary>
		/// Custom handler for file errors.
		/// </summary>
		public Action<FileError> HandleFileError {
			get => Options.HandleFileError;
			set { Options.HandleFileError = value; } 
		}

		public FileLoggerProvider(string fileName) : this(fileName, true) {
		}

		public FileLoggerProvider(string fileName, bool append) : this(fileName, new FileLoggerOptions() { Append = append } ) {
		}

		public FileLoggerProvider(string fileName, FileLoggerOptions options) {
			Options = options;
			LogFileName = Environment.ExpandEnvironmentVariables(fileName);

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
			var writeMessageFailed = false;
			foreach (var message in entryQueue.GetConsumingEnumerable()) {
				try {
					if (!writeMessageFailed)
						fWriter.WriteMessage(message, entryQueue.Count == 0);
				} catch (Exception ex) {
					// something goes wrong. App's code can handle it if 'HandleFileError' is provided
					var stopLogging = true;
					if (HandleFileError != null) {
						var fileErr = new FileError(LogFileName, ex);
						try {
							HandleFileError(fileErr);
							if (fileErr.NewLogFileName != null) {
								fWriter.UseNewLogFile(fileErr.NewLogFileName);
								// write failed message to a new log file
								fWriter.WriteMessage(message, entryQueue.Count == 0);
								stopLogging = false;
							}
						} catch {
							// exception is possible in HandleFileError or if proposed file name cannot be used
							// let's ignore it in that case -> file logger will stop processing log messages
						}
					}
					if (stopLogging) {
						// Stop processing log messages since they cannot be written to a log file
						entryQueue.CompleteAdding();
						writeMessageFailed = true;
					}
				}
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

			void createLogFileStream(bool append) {
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

			internal void UseNewLogFile(string newLogFileName) {
				FileLogPrv.LogFileName = newLogFileName;
				DetermineLastFileLogName(); // preserve all existing logic related to 'FormatLogFileName' and rolling files
				createLogFileStream(FileLogPrv.Append);  // if file error occurs here it is not handled by 'HandleFileError' recursively
			}

			void OpenFile(bool append) {
				try {
					createLogFileStream(append);
				} catch (Exception ex) {
					if (FileLogPrv.HandleFileError!=null) {
						var fileErr = new FileError(LogFileName, ex);
						FileLogPrv.HandleFileError(fileErr);
						if (fileErr.NewLogFileName!=null) {
							UseNewLogFile(fileErr.NewLogFileName);
						}
					} else {
						throw; // do not handle by default to preserve backward compatibility
					}
				}
			}


			string GetNextFileLogName() {
				var baseLogFileName = GetBaseLogFileName();
				// if file does not exist or file size limit is not reached - do not add rolling file index
				if (!System.IO.File.Exists(baseLogFileName) || 
					FileLogPrv.FileSizeLimitBytes <= 0 || 
					new System.IO.FileInfo(baseLogFileName).Length< FileLogPrv.FileSizeLimitBytes)
					return baseLogFileName;

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
				bool openNewFile = false;
				if (isMaxFileSizeThresholdReached() || isBaseFileNameChanged())
					openNewFile = true;

				if (openNewFile) {
					Close();
					LogFileName = GetNextFileLogName();
					OpenFile(false);
				}

				bool isMaxFileSizeThresholdReached() {
					return FileLogPrv.FileSizeLimitBytes > 0 && LogFileStream.Length > FileLogPrv.FileSizeLimitBytes;
				}
				bool isBaseFileNameChanged() {
					if (FileLogPrv.FormatLogFileName!=null) {
						var baseLogFileName = GetBaseLogFileName();
						if (baseLogFileName!=__LastBaseLogFileName) {
							__LastBaseLogFileName = baseLogFileName;
							return true;
						}
						return false;
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

		/// <summary>
		/// Represents a file error context.
		/// </summary>
		public class FileError {

			/// <summary>
			/// Exception that occurs on the file operation.
			/// </summary>
			public Exception ErrorException { get; private set; }

			/// <summary>
			/// Current log file name.
			/// </summary>
			public string LogFileName { get; private set; }

			internal FileError(string logFileName, Exception ex) {
				LogFileName = logFileName;
				ErrorException = ex;
			}

			internal string NewLogFileName { get; private set; }

			/// <summary>
			/// Suggests a new log file name to use instead of the current one. 
			/// </summary>
			/// <remarks>
			/// If proposed file name also leads to a file error this will break a file logger: errors are not handled recursively.
			/// </remarks>
			/// <param name="newLogFileName">a new log file name</param>
			public void UseNewLogFileName(string newLogFileName) {
				NewLogFileName = newLogFileName;
			}
		}

	}

}
