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
	public class FileLoggerProvider : ILoggerProvider {

		private readonly string LogFileName;
		Stream LogFileStream;
		TextWriter LogFileWriter;
		object fileLock = new object();

		private readonly ConcurrentDictionary<string, FileLogger> loggers =
			new ConcurrentDictionary<string, FileLogger>();
		private readonly BlockingCollection<string> entryQueue = new BlockingCollection<string>(1024);
		private readonly Task processQueueTask;

		private readonly bool Append = true;

		public LogLevel MinLevel { get; set; } = LogLevel.Debug;

		/// <summary>
		/// Custom formatter for log entry. 
		/// </summary>
		public Func<LogMessage,string> FormatLogEntry { get; set; }

		public FileLoggerProvider(string fileName) : this(fileName, true) {

		}

		public FileLoggerProvider(string fileName, bool append) {
			LogFileName = fileName;
			Append = append;
			LogFileStream = new FileStream(LogFileName, FileMode.OpenOrCreate, FileAccess.Write);
			if (Append)
				LogFileStream.Seek(0, SeekOrigin.End);
			LogFileWriter = new StreamWriter(LogFileStream);

			processQueueTask = Task.Factory.StartNew(
				ProcessQueue,
				this,
				TaskCreationOptions.LongRunning);
		}

		public ILogger CreateLogger(string categoryName) {
			return loggers.GetOrAdd(categoryName, CreateLoggerImplementation);
		}

		private void WriteMessage(string message, bool flush) {
			if (LogFileWriter!=null) {
				LogFileWriter.WriteLine(message);
				if (flush)
					LogFileWriter.Flush();
			}
		}

		public void Dispose() {
			entryQueue.CompleteAdding();
			try {
				processQueueTask.Wait(1500);  // the same as in ConsoleLogger
			} catch (TaskCanceledException) { } catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException) { }

			loggers.Clear();

			var logWriter = LogFileWriter;
			LogFileWriter = null;
			logWriter.Dispose();
			LogFileStream.Dispose();
			LogFileStream = null;
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
			// use lock and write directly to a file?..
			lock (fileLock) {
				System.IO.File.AppendAllText(LogFileName, message+"\n");
			}
		}
		private void ProcessQueue() {
			foreach (var message in entryQueue.GetConsumingEnumerable()) {
				WriteMessage(message, entryQueue.Count==0);
			}
		}

		private static void ProcessQueue(object state) {
			var consoleLogger = (FileLoggerProvider)state;
			consoleLogger.ProcessQueue();
		}

	}

}
