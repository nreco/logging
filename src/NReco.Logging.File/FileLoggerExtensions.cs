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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NReco.Logging.File {

	public static class FileLoggerExtensions {

		/// <summary>
		/// Adds a file logger.
		/// </summary>
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string fileName, bool append = true) {
			builder.Services.Add(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(
				(srvPrv) => {
					return new FileLoggerProvider(fileName, append);
				}
			));
			return builder;
		}

		/// <summary>
		/// Adds a file logger.
		/// </summary>
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string fileName, Action<FileLoggerOptions> configure) {
			builder.Services.Add(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(
				(srvPrv) => {
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
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder, IConfiguration configuration, Action<FileLoggerOptions> configure = null) {
			var fileLoggerPrv = CreateFromConfiguration(configuration, configure);
			if (fileLoggerPrv != null) {
				builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(
					(srvPrv) => {
						return fileLoggerPrv;
					}
				);
			}
			return builder;
		}

		/// <summary>
		/// Adds a file logger.
		/// </summary>
		/// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
		/// <param name="fileName">log file name.</param>
		/// <param name="append">if true new log entries are appended to the existing file.</param>	 
		public static ILoggerFactory AddFile(this ILoggerFactory factory, string fileName, bool append = true) {
			factory.AddProvider(new FileLoggerProvider(fileName, append));
			return factory;
		}

		/// <summary>
		/// Adds a file logger.
		/// </summary>
		/// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
		/// <param name="fileName">log file name.</param>
		/// <param name="configure">a handler that initializes <see cref="FileLoggerOptions"/>.</param>
		public static ILoggerFactory AddFile(this ILoggerFactory factory, string fileName, Action<FileLoggerOptions> configure) {
			var fileLoggerOptions = new FileLoggerOptions();
			configure(fileLoggerOptions);
			factory.AddProvider(new FileLoggerProvider(fileName, fileLoggerOptions));
			return factory;
		}

		/// <summary>
		/// Adds a file logger and configures it with given <see cref="IConfiguration"/> (usually "Logging" section).
		/// </summary>
		/// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
		/// <param name="configuration">The <see cref="IConfiguration"/> to use getting <see cref="FileLoggerProvider"/> settings.</param>
		/// <param name="configure">a handler that initializes <see cref="FileLoggerOptions"/>.</param>
		public static ILoggerFactory AddFile(this ILoggerFactory factory, IConfiguration configuration, Action<FileLoggerOptions> configure = null) {
			var prvFactory = factory;
			var fileLoggerPrv = CreateFromConfiguration(configuration, configure);
			if (fileLoggerPrv == null)
				return factory;
			prvFactory.AddProvider(fileLoggerPrv);
			return factory;
		}

		private static FileLoggerProvider CreateFromConfiguration(IConfiguration configuration, Action<FileLoggerOptions> configure) {
			var config = new FileLoggerConfig();
			var fileSection = configuration.GetSection("File");
			if (!fileSection.Exists()) {
				var pathValue = configuration["Path"];
				if (String.IsNullOrEmpty(pathValue))
					return null;  // file logger is not configured
				else {
					// configuration contains "Path" property so this is explicitly-passed configuration section
					configuration.Bind(config);
				}
			} else {
				fileSection.Bind(config);
			}

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
