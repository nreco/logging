#region License
/*
 * NReco file logging provider (https://github.com/nreco/logging)
 * Copyright 2017-2018 Vitaliy Fedorchenko
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
	/// Generic file logger options.
	/// </summary>
	public class FileLoggerOptions
	{
		/// <summary>
		/// Append to existing log files or override them.
		/// </summary>
		public bool Append { get; set; } = true;

		/// <summary>
		/// Determines max size of the one log file.
		/// </summary>
		/// <remarks>If log file limit is specified logger will create new file when limit is reached. 
		/// For example, if log file name is 'test.log', logger will create 'test1.log', 'test2.log' etc.
		/// </remarks>
		public long FileSizeLimitBytes { get; set; } = 0;

		/// <summary>
		/// Determines max number of log files if <see cref="FileSizeLimitBytes"/> is specified.
		/// </summary>
		/// <remarks>If MaxRollingFiles is specified file logger will re-write previously created log files.
		/// For example, if log file name is 'test.log' and max files = 3, logger will use: 'test.log', then 'test1.log', then 'test2.log' and then 'test.log' again (old content is removed).
		/// </remarks>
		public int MaxRollingFiles { get; set; } = 0;

		/// <summary>
		///  Gets or sets indication whether or not UTC timezone should be used to for timestamps in logging messages. Defaults to false.
		/// </summary>
		public bool UseUtcTimestamp { get; set; }

		/// <summary>
		/// Custom formatter for the log entry line. 
		/// </summary>
		public Func<LogMessage, string> FormatLogEntry { get; set; }

		/// <summary>
		/// Custom filter for the log entry.
		/// </summary>
		public Func<LogMessage, bool> FilterLogEntry { get; set; }

		/// <summary>
		/// Minimal logging level for the file logger.
		/// </summary>
		public LogLevel MinLevel { get; set; } = LogLevel.Trace;

		/// <summary>
		/// Custom formatter for the log file name.
		/// </summary>
		/// <remarks>By specifying custom formatting handler you can define your own criteria for creation of log files. Note that this handler is called
		/// on EVERY log message 'write'; you may cache the log file name calculation in your handler to avoid any potential overhead in case of high-load logger usage.
		/// For example:
		/// </remarks>
		/// <example>
		/// fileLoggerOpts.FormatLogFileName = (fname) => {
		///   return String.Format( Path.GetFileNameWithoutExtension(fname) + "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname), DateTime.UtcNow); 
		/// };
		/// </example>
		public Func<string, string> FormatLogFileName { get; set; }

		/// <summary>
		/// Custom handler for log file errors.
		/// </summary>
		/// <remarks>If this handler is provided file open exception (on <code>FileLoggerProvider</code> creation) will be suppressed.
		/// You can handle file error exception according to your app's logic, and propose an alternative log file name (if you want to keep file logger working).
		/// </remarks>
		/// <example>
		/// fileLoggerOpts.HandleFileError = (err) => {
		///   err.UseNewLogFileName( Path.GetFileNameWithoutExtension(err.LogFileName)+ "_alt" + Path.GetExtension(err.LogFileName) );
		/// };
		/// </example>
		public Action<FileLoggerProvider.FileError> HandleFileError { get; set; }

	}


}
