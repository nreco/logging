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

namespace NReco.Logging.File
{

    /// <summary>
    /// Generic file logger that works in a similar way to standard ConsoleLogger.
    /// </summary>
    public class FileLogger : ILogger
    {

        private readonly string logName;
        private readonly FileLoggerProvider LoggerPrv;

        /// <summary>
        /// Create new instance
        /// </summary>
        /// <param name="logName">Log file name</param>
        /// <param name="loggerPrv"></param>
        public FileLogger(string logName, FileLoggerProvider loggerPrv)
        {
            this.logName = logName;
            this.LoggerPrv = loggerPrv;
        }

        /// <summary>
        /// Create a new logging scope (nop)
        /// </summary>
        /// <typeparam name="TState">Arbitrary scope object type</typeparam>
        /// <param name="state">Logging scope state object</param>
        /// <returns>NULL</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        /// <summary>
        /// Determine if the specified log level should be written to output
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns>True if logLevel is greater than <see cref="FileLoggerProvider.MinLevel"/>, false otherwise </returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LoggerPrv.MinLevel;
        }

        string GetShortLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
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

        /// <summary>
        /// Write message to log output
        /// </summary>
        /// <typeparam name="TState">Log category</typeparam>
        /// <param name="logLevel">Log level</param>
        /// <param name="eventId">Event ID</param>
        /// <param name="state">Log state object</param>
        /// <param name="exception">Log exception</param>
        /// <param name="formatter">Log formatter</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }
            string message = null;
            if (null != formatter)
            {
                message = formatter(state, exception);
            }

            if (LoggerPrv.FormatLogEntry != null)
            {
                LoggerPrv.WriteEntry(LoggerPrv.FormatLogEntry(
                    new LogMessage(logName, logLevel, eventId, message, exception)));
            }
            else
            {
                // default formatting logic
                var logBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(message))
                {
                    _ = logBuilder
                        .Append(DateTime.Now.ToString("o"))
                        .Append('\t')
                        .Append(GetShortLogLevel(logLevel))
                        .Append("\t[")
                        .Append(logName)
                        .Append("]")
                        .Append("\t[")
                        .Append(eventId)
                        .Append("]\t")
                        .Append(message);
                }

                if (exception != null)
                {
                    // exception message
                    _ = logBuilder.AppendLine(exception.ToString());
                }
                LoggerPrv.WriteEntry(logBuilder.ToString());
            }
        }
    }


}
