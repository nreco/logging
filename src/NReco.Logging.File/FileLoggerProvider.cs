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
using LocalLogging = Nreco.Logging.File.Microsoft.Extensions.Logging;

namespace NReco.Logging.File
{

    /// <summary>
    /// Generic file logger provider.
    /// </summary>
#if NETSTANDARD2
    [ProviderAlias("File")]
#endif
    public class FileLoggerProvider : ILoggerProvider, LocalLogging::ISupportExternalScope
    {

        private readonly string LogFileName;

        private readonly ConcurrentDictionary<string, FileLogger> loggers =
            new ConcurrentDictionary<string, FileLogger>();
        private readonly BlockingCollection<string> entryQueue = new BlockingCollection<string>(1024);
        private readonly Task processQueueTask;
        private readonly FileWriter fWriter;

        private readonly bool Append = true;
        private readonly long FileSizeLimitBytes = 0;
        private readonly int MaxRollingFiles = 0;

        private LocalLogging::IExternalScopeProvider scopeProvider = new LocalLogging.LoggerExternalScopeProvider();

        /// <summary>
        /// The minimum log level that will be written to the output
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// Custom formatter for log entry. 
        /// </summary>
        public Func<LogMessage, string> FormatLogEntry { get; set; }

        /// <summary>
        /// Custom formatter for the log file name.
        /// </summary>
        public Func<string, string> FormatLogFileName { get; set; }

        /// <summary>
        /// Create new instance
        /// </summary>
        /// <param name="fileName">Log output file name</param>
        public FileLoggerProvider(string fileName) : this(fileName, true)
        {
        }

        /// <summary>
        /// Create new instance
        /// </summary>
        /// <param name="fileName">Output file name</param>
        /// <param name="append">Determine if multiple log messages should be added to a single file</param>
        public FileLoggerProvider(string fileName, bool append) : this(fileName, new FileLoggerOptions() { Append = append })
        {
        }

        /// <summary>
        /// Create new instance
        /// </summary>
        /// <param name="fileName">Output file name</param>
        /// <param name="options">Logger options</param>
        public FileLoggerProvider(string fileName, FileLoggerOptions options)
        {
            LogFileName = Environment.ExpandEnvironmentVariables(fileName);
            Append = options.Append;
            FileSizeLimitBytes = options.FileSizeLimitBytes;
            MaxRollingFiles = options.MaxRollingFiles;
            FormatLogEntry = options.FormatLogEntry;
            FormatLogFileName = options.FormatLogFileName;

            fWriter = new FileWriter(this);
            processQueueTask = Task.Factory.StartNew(
                ProcessQueue,
                this,
                TaskCreationOptions.LongRunning);
        }

        void LocalLogging::ISupportExternalScope.SetScopeProvider(LocalLogging::IExternalScopeProvider scopeProvider)
        {
            this.scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Get or create a tracked logger for specified category
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns>Logger associated with the specified category</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
        }

        /// <summary>
        /// Wait for pending logs to be flushed to output and remove tracked loggers
        /// </summary>
        public void Dispose()
        {
            entryQueue.CompleteAdding();
            try
            {
                _ = processQueueTask.Wait(1500);  // the same as in ConsoleLogger
            }
            catch (TaskCanceledException) { }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

            loggers.Clear();
            fWriter.Close();
        }

        private FileLogger CreateLoggerImplementation(string name)
        {
            return new FileLogger(name, this, scopeProvider);
        }

        internal void WriteEntry(string message)
        {
            if (!entryQueue.IsAddingCompleted)
            {
                try
                {
                    entryQueue.Add(message);
                    return;
                }
                catch (InvalidOperationException) { }
            }
            // do nothing
        }
        private void ProcessQueue()
        {
            foreach (var message in entryQueue.GetConsumingEnumerable())
            {
                fWriter.WriteMessage(message, entryQueue.Count == 0);
            }
        }

        private static void ProcessQueue(object state)
        {
            var fileLogger = (FileLoggerProvider)state;
            fileLogger.ProcessQueue();
        }

        internal class FileWriter
        {

            readonly FileLoggerProvider FileLogPrv;
            string LogFileName;
            Stream LogFileStream;
            TextWriter LogFileWriter;

            internal FileWriter(FileLoggerProvider fileLogPrv)
            {
                FileLogPrv = fileLogPrv;

                DetermineLastFileLogName();
                OpenFile(FileLogPrv.Append);
            }

            string GetBaseLogFileName()
            {
                var fName = FileLogPrv.LogFileName;
                if (FileLogPrv.FormatLogFileName != null)
                    fName = FileLogPrv.FormatLogFileName(fName);
                return fName;
            }

            void DetermineLastFileLogName()
            {
                var baseLogFileName = GetBaseLogFileName();
                __LastBaseLogFileName = baseLogFileName;
                if (FileLogPrv.FileSizeLimitBytes > 0)
                {
                    // rolling file is used
                    var logFileMask = Path.GetFileNameWithoutExtension(baseLogFileName) + "*" + Path.GetExtension(baseLogFileName);
                    var logDirName = Path.GetDirectoryName(baseLogFileName);
                    if (String.IsNullOrEmpty(logDirName))
                        logDirName = Directory.GetCurrentDirectory();
                    var logFiles = Directory.Exists(logDirName) ? Directory.GetFiles(logDirName, logFileMask, SearchOption.TopDirectoryOnly) : Array.Empty<string>();
                    if (logFiles.Length > 0)
                    {
                        var lastFileInfo = logFiles
                                .Select(fName => new FileInfo(fName))
                                .OrderByDescending(fInfo => fInfo.Name)
                                .OrderByDescending(fInfo => fInfo.LastWriteTime).First();
                        LogFileName = lastFileInfo.FullName;
                    }
                    else
                    {
                        // no files yet, use default name
                        LogFileName = baseLogFileName;
                    }
                }
                else
                {
                    LogFileName = baseLogFileName;
                }
            }

            void OpenFile(bool append)
            {
                var fileInfo = new FileInfo(LogFileName);

                // Directory.Create will check if the directory already exists,
                // so there is no need for a "manual" check first.
                fileInfo.Directory.Create();

                LogFileStream = new FileStream(LogFileName, FileMode.OpenOrCreate, FileAccess.Write);
                if (append)
                {
                    _ = LogFileStream.Seek(0, SeekOrigin.End);
                }
                else
                {
                    LogFileStream.SetLength(0); // clear the file
                }
                LogFileWriter = new StreamWriter(LogFileStream);
            }

            string GetNextFileLogName()
            {
                var baseLogFileName = GetBaseLogFileName();
                // if file does not exist or file size limit is not reached - do not add rolling file index
                if (!System.IO.File.Exists(baseLogFileName) ||
                    FileLogPrv.FileSizeLimitBytes <= 0 ||
                    new System.IO.FileInfo(baseLogFileName).Length < FileLogPrv.FileSizeLimitBytes)
                    return baseLogFileName;

                int currentFileIndex = 0;
                var baseFileNameOnly = Path.GetFileNameWithoutExtension(baseLogFileName);
                var currentFileNameOnly = Path.GetFileNameWithoutExtension(LogFileName);

                var suffix = currentFileNameOnly.Substring(baseFileNameOnly.Length);
                if (suffix.Length > 0 && Int32.TryParse(suffix, out var parsedIndex))
                {
                    currentFileIndex = parsedIndex;
                }
                var nextFileIndex = currentFileIndex + 1;
                if (FileLogPrv.MaxRollingFiles > 0)
                {
                    nextFileIndex %= FileLogPrv.MaxRollingFiles;
                }

                var nextFileName = baseFileNameOnly + (nextFileIndex > 0 ? nextFileIndex.ToString() : "") + Path.GetExtension(baseLogFileName);
                return Path.Combine(Path.GetDirectoryName(baseLogFileName), nextFileName);
            }

            // cache last returned base log file name to avoid excessive checks in CheckForNewLogFile.isBaseFileNameChanged
            string __LastBaseLogFileName = null;

            void CheckForNewLogFile()
            {
                bool openNewFile = false;
                if (isMaxFileSizeThresholdReached() || isBaseFileNameChanged())
                    openNewFile = true;

                if (openNewFile)
                {
                    Close();
                    LogFileName = GetNextFileLogName();
                    OpenFile(false);
                }

                bool isMaxFileSizeThresholdReached()
                {
                    return FileLogPrv.FileSizeLimitBytes > 0 && LogFileStream.Length > FileLogPrv.FileSizeLimitBytes;
                }
                bool isBaseFileNameChanged()
                {
                    if (FileLogPrv.FormatLogFileName != null)
                    {
                        var baseLogFileName = GetBaseLogFileName();
                        if (baseLogFileName != __LastBaseLogFileName)
                        {
                            __LastBaseLogFileName = baseLogFileName;
                            return true;
                        }
                        return false;
                    }
                    return false;
                }
            }

            internal void WriteMessage(string message, bool flush)
            {
                if (LogFileWriter != null)
                {
                    CheckForNewLogFile();
                    LogFileWriter.WriteLine(message);
                    if (flush)
                        LogFileWriter.Flush();
                }
            }

            internal void Close()
            {
                if (LogFileWriter != null)
                {
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
