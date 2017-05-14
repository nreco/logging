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

using NReco.Logging.File;

namespace Microsoft.Extensions.Logging {

	public static class FileLoggerExtensions {

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
		/// Adds a file logger and configures it with given <see cref="IConfiguration"/> (usually "Logging" section).
		/// </summary>
		/// <param name="factory">The <see cref="ILoggerFactory"/> to use.</param>
		/// <param name="configuration">The <see cref="IConfiguration"/> to use getting <see cref="FileLoggerProvider"/> settings.</param>
		public static ILoggerFactory AddFile(this ILoggerFactory factory, IConfiguration configuration) {
			var prvFactory = factory;
			
			var fileSection = configuration.GetSection("File");
			if (fileSection==null)
				return factory;  // file logger is not configured
			var fileName = fileSection["Path"];
			if (String.IsNullOrWhiteSpace(fileName))
				return factory; // file logger is not configured

			var append = true;
			var appendVal = fileSection["Append"];
			if (!String.IsNullOrEmpty(appendVal))
				append = bool.Parse(appendVal);

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

			prvFactory.AddProvider(new FileLoggerProvider(fileName, append));
			return factory;
		}

	}

}
