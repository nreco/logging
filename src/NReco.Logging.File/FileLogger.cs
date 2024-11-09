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
using NReco.Logging.File.Extensions;
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
			return logLevel switch {
				LogLevel.Trace => "TRCE",
				LogLevel.Debug => "DBUG",
				LogLevel.Information => "INFO",
				LogLevel.Warning => "WARN",
				LogLevel.Error => "FAIL",
				LogLevel.Critical => "CRIT",
				LogLevel.None => "NONE",
				_ => logLevel.ToString().ToUpper(),
			};
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
				const int MaxStackAllocatedBufferLength = 256;
				var logMessageLength = CalculateLogMessageLength(eventId, message);
				char[] charBuffer = null;
				try {
					Span<char> buffer = logMessageLength <= MaxStackAllocatedBufferLength
						? stackalloc char[MaxStackAllocatedBufferLength]
						: (charBuffer = ArrayPool<char>.Shared.Rent(logMessageLength));

					FormatLogEntryDefault(buffer, message, logLevel, eventId, exception);
				}
				finally {
					if (charBuffer is not null) {
						ArrayPool<char>.Shared.Return(charBuffer);
					}
				}
			}
		}

		private void FormatLogEntryDefault(Span<char> buffer, string message, LogLevel logLevel,
			EventId eventId, Exception exception) {
			// default formatting logic
			using var logBuilder = new ValueStringBuilder(buffer);
			if (!string.IsNullOrEmpty(message)) {
				DateTime timeStamp = LoggerPrv.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
				timeStamp.TryFormatO(logBuilder.RemainingRawChars, out var charsWritten);
				logBuilder.AppendSpan(charsWritten);
				logBuilder.Append('\t');
				logBuilder.Append(GetShortLogLevel(logLevel));
				logBuilder.Append("\t[");
				logBuilder.Append(logName);
				logBuilder.Append("]\t[");
				if (eventId.Name is not null) {
					logBuilder.Append(eventId.Name);
				}
				else {
					eventId.Id.TryFormat(logBuilder.RemainingRawChars, out charsWritten);
					logBuilder.AppendSpan(charsWritten);
				}
				logBuilder.Append("]\t");
				logBuilder.Append(message);
			}

			if (exception != null) {
				// exception message
				logBuilder.Append(exception.ToString());
				logBuilder.Append(Environment.NewLine);
			}
			LoggerPrv.WriteEntry(logBuilder.ToString());
		}

		private int CalculateLogMessageLength(EventId eventId, string message) {
			return 33 /* timeStamp.TryFormatO */
				+ 1 /* '\t' */
				+ 4 /* GetShortLogLevel */
				+ 2 /* "\t[" */
				+ (logName?.Length ?? 0)
				+ 3 /* "]\t[" */
				+ (eventId.Name?.Length ?? eventId.Id.GetFormattedLength())
				+ 2 /* "]\t" */
				+ (message?.Length ?? 0);
		}
	}
}
