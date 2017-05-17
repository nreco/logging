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
	/// Generic file logger that works in a similar way to standard ConsoleLogger.
	/// </summary>
	public class FileLogger : ILogger {

		private readonly string logName;
		private readonly FileLoggerProvider LoggerPrv;

		public FileLogger(string logName, FileLoggerProvider loggerPrv) {
			this.logName = logName;
			this.LoggerPrv = loggerPrv;
		}
		public IDisposable BeginScope<TState>(TState state) {
			return null;
		}

		public bool IsEnabled(LogLevel logLevel) {
			return logLevel>=LoggerPrv.MinLevel;
		}

		string GetShortLogLevel(LogLevel logLevel) {
			switch (logLevel) {
				case LogLevel.Trace:
					return "TRCE";
				case LogLevel.Debug:
					return "DBUG";
				case LogLevel.Information:
					return "INFO";
				case LogLevel.Warning:
					return "WARN";
				case LogLevel.Error:
					return "FAIL";
				case LogLevel.Critical:
					return "CRIT";
			}
			return logLevel.ToString().ToUpper();
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
			Exception exception, Func<TState, Exception, string> formatter) {
			if (!IsEnabled(logLevel)) {
				return;
			}

			if (formatter == null) {
				throw new ArgumentNullException(nameof(formatter));
			}
			string message = null;
			if (null != formatter) {
				message = formatter(state, exception);
			}

			if (LoggerPrv.FormatLogEntry!=null) {
				LoggerPrv.WriteEntry(LoggerPrv.FormatLogEntry(
					new LogMessage(logLevel, eventId, message, exception)));
			} else {
				// default formatting logic
				var logBuilder = new StringBuilder();
				if (!string.IsNullOrEmpty(message)) {
					logBuilder.Append(DateTime.Now.ToString("o"));
					logBuilder.Append('\t');
					logBuilder.Append(GetShortLogLevel(logLevel));
					logBuilder.Append("\t[");
					logBuilder.Append(logName);
					logBuilder.Append("]");
					logBuilder.Append("\t[");
					logBuilder.Append(eventId);
					logBuilder.Append("]\t");
					logBuilder.Append(message);
				}

				if (exception != null) {
					// exception message
					logBuilder.AppendLine(exception.ToString());
				}
				LoggerPrv.WriteEntry(logBuilder.ToString());
			}
		}
	}


}
