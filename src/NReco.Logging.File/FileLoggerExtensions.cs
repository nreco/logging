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
using System.IO;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NReco.Logging.File;

namespace Microsoft.Extensions.Logging
{

    public static class FileLoggerExtensions
    {

#if NETSTANDARD2

        /// <summary>
        /// Adds a file logger.
        /// </summary>
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string fileName, bool append = true)
        {
            builder.Services.Add(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(
                (srvPrv) =>
                {
                    return new FileLoggerProvider(fileName, append);
                }
            ));
            return builder;
        }

        /// <summary>
        /// Adds a file logger.
        /// </summary>
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string fileName, Action<FileLoggerOptions> configure)
        {
            builder.Services.Add(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(
                (srvPrv) =>
                {
                    var options = new FileLoggerOptions();
                    configure(options);
                    return new FileLoggerProvider(fileName, options);
                }
            ));
            return builder;
        }

        /// <summary>
        /// Adds a file logger by specified configuration.
        /// </summary>
        /// <remarks>File logger is not added if "File" section is not present or it doesn't contain "Path" property.</remarks>
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, IConfiguration configuration, Action<FileLoggerOptions> configure = null)
        {
            var fileLoggerPrv = CreateFromConfiguration(configuration, configure);
            if (fileLoggerPrv != null)
            {
                builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(
                    (srvPrv) =>
                    {
                        return fileLoggerPrv;
                    }
                );
            }
            return builder;
        }

#endif

        /// <summary>
        /// Adds a file logger.
        /// </summary>
        /// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
        /// <param name="fileName">log file name.</param>
        /// <param name="append">if true new log entries are appended to the existing file.</param>	 
        public static ILoggerFactory AddFile(this ILoggerFactory factory, string fileName, bool append = true)
        {
            factory.AddProvider(new FileLoggerProvider(fileName, append));
            return factory;
        }

        /// <summary>
        /// Adds a file logger and configures it with given <see cref="IConfiguration"/> (usually "Logging" section).
        /// </summary>
        /// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
        /// <param name="configuration">The <see cref="IConfiguration"/> to use getting <see cref="FileLoggerProvider"/> settings.</param>
        public static ILoggerFactory AddFile(this ILoggerFactory factory, IConfiguration configuration, Action<FileLoggerOptions> configure = null)
        {
            var prvFactory = factory;
            var fileLoggerPrv = CreateFromConfiguration(configuration, configure);
            if (fileLoggerPrv == null)
                return factory;
#if NETSTANDARD1
			var loggerSettings = new FilterLoggerSettings();
			var logLevelsCfg = configuration.GetSection("LogLevel");
			bool hasFilter = false;
			if (logLevelsCfg!=null) {
				var logLevels = logLevelsCfg.GetChildren();
				foreach (var logLevel in logLevels) {
					var logLevelValue = default(LogLevel);
					Enum.TryParse(logLevel.Value, ignoreCase: true, result: out logLevelValue);
					loggerSettings.Add(logLevel.Key, logLevelValue);
					hasFilter = true;
				}
			}
			if (hasFilter)
				prvFactory = prvFactory.WithFilter(loggerSettings);
#endif

            prvFactory.AddProvider(fileLoggerPrv);
            return factory;
        }

        private static FileLoggerProvider CreateFromConfiguration(IConfiguration configuration, Action<FileLoggerOptions> configure)
        {
            var fileSection = configuration.GetSection("File");
            if (fileSection == null)
                return null;  // file logger is not configured

            var config = new FileLoggerConfig();
            fileSection.Bind(config);

            if (String.IsNullOrWhiteSpace(config.Path))
                return null; // file logger is not configured

            var fileLoggerOptions = new FileLoggerOptions();

            fileLoggerOptions.Append = config.Append;
            fileLoggerOptions.MinLevel = config.MinLevel;
            fileLoggerOptions.FileSizeLimitBytes = config.FileSizeLimitBytes;
            fileLoggerOptions.MaxRollingFiles = config.MaxRollingFiles;

            if (configure != null)
                configure(fileLoggerOptions);

            return new FileLoggerProvider(config.Path, fileLoggerOptions);
        }


    }

}
