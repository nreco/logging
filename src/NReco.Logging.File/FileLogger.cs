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
using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging;

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

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
			Exception exception, Func<TState, Exception, string> formatter) {
			if (!IsEnabled(logLevel)) {
				return;
			}

			if (formatter == null) {
				throw new ArgumentNullException(nameof(formatter));
			}

			string message = formatter(state, exception);

			if (LoggerPrv.Options.FilterLogEntry != null)
				if (!LoggerPrv.Options.FilterLogEntry(new LogMessage(logName, logLevel, eventId, message, exception)))
					return;

			if (LoggerPrv.FormatLogEntry != null) {
				LoggerPrv.WriteEntry(LoggerPrv.FormatLogEntry(
					new LogMessage(logName, logLevel, eventId, message, exception)));
			}
			else {
				LoggerPrv.WriteEntry( 
					Format.StringLogEntryFormatter.Instance.LowAllocLogEntryFormat(
						logName,
						LoggerPrv.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now,
						logLevel,
						eventId,
						message,
						exception));
			}
		}

	}
}
