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
using System.Reflection;
using System.Linq.Expressions;
using LocalLogging = Nreco.Logging.File.Microsoft.Extensions.Logging;

namespace NReco.Logging.File
{

    /// <summary>
    /// Generic file logger that works in a similar way to standard ConsoleLogger.
    /// </summary>
    public class FileLogger : ILogger
    {

        private readonly string logName;
        private readonly FileLoggerProvider LoggerPrv;

        internal LocalLogging::IExternalScopeProvider ScopeProvider { get; set; }

        /// <summary>
        /// Create new instance
        /// </summary>
        /// <param name="logName">Log file name</param>
        /// <param name="loggerPrv">Logger provider</param>
        /// <param name="scopeProvider">Scope provider</param>
        public FileLogger(string logName, FileLoggerProvider loggerPrv, LocalLogging::IExternalScopeProvider scopeProvider)
        {
            this.logName = logName;
            this.LoggerPrv = loggerPrv;
            this.ScopeProvider = scopeProvider;
        }

        /// <summary>
        /// Create a new logging scope
        /// </summary>
        /// <typeparam name="TState">Arbitrary scope object type</typeparam>
        /// <param name="state">Logging scope state object</param>
        /// <returns>New logging scope context</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return ScopeProvider?.Push(state) ?? NullScope.Instance;
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
                var logObj = new LogMessage(logName, logLevel, eventId, message, exception);
                // Append the data of all BeginScope and LogXXX parameters to the message dictionary
                AppendScope(
                    logObj.ScopeList,
                    logObj.ScopeArgs,
                    state);

                LoggerPrv.WriteEntry(LoggerPrv.FormatLogEntry(logObj));
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


        private void AppendScope<TState>(
            List<object> scopeList,
            IDictionary<string, object> scopeProperties,
            TState state)
        {
            ScopeProvider.ForEachScope((scope, state2) => AppendScope(scopeList, scopeProperties, scope), state);
        }

        /// <summary>
        /// Add scope objects to the proper log property
        /// </summary>
        /// <param name="scopeList"></param>
        /// <param name="scopeProperties"></param>
        /// <param name="scope"></param>
        /// <remarks>
        /// Sematic reference
        /// https://nblumhardt.com/2016/11/ilogger-beginscope/
        /// </remarks>
        private static void AppendScope(
            List<object> scopeList,
            IDictionary<string, object> scopeProperties,
            object scope)
        {
            if (scope == null)
                return;

            if (scope is NullScope)
                return;

            // The scope can be defined using BeginScope or LogXXX methods.
            // - logger.BeginScope(new { Author = "meziantou" })
            // - logger.LogInformation("Hello {Author}", "meziaantou")
            // Using LogXXX, an object of type FormattedLogValues is created. This type is internal but it implements IReadOnlyList, so we can use it.
            // https://github.com/aspnet/Extensions/blob/cc9a033c6a8a4470984a4cc8395e42b887c07c2e/src/Logging/Logging.Abstractions/src/FormattedLogValues.cs
            if (scope is IEnumerable<KeyValuePair<string, object>> formattedLogValues)
            {
                var strTemplate = new StringBuilder();
                foreach (var value in formattedLogValues)
                {
                    // MethodInfo is set by ASP.NET Core when reaching a controller. This type cannot be serialized using JSON.NET, but I don't need it.
                    if (value.Value is MethodInfo)
                        continue;

                    if (value.Key == "{OriginalFormat}")
                    {
                        if (value.Value is string strTmp)
                        {
                            strTemplate.Append(strTmp);
                        }
                    }
                    else
                    {
                        scopeProperties[value.Key] = value.Value;
                    }
                }
                if (strTemplate.Length > 0)
                {
                    foreach (var scopeArg in scopeProperties)
                    {
                        _ = strTemplate.Replace("{" + scopeArg.Key + "}", scopeArg.Value.ToString());
                    }
                    scopeList.Add(strTemplate.ToString());
                }
            }
            else
            {
                scopeList.Add(scope);
            }
        }

    }


}
