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
using Microsoft.Extensions.Logging;

namespace NReco.Logging.File {

	public readonly struct LogMessage {
		public readonly string LogName;
		public readonly string Message;
		public readonly LogLevel LogLevel;
		public readonly EventId EventId;
		public readonly Exception Exception;

		/// <summary>
		/// Provides access to the active logging scopes, or <c>null</c> when scopes are unavailable.
		/// </summary>
		/// <remarks>
		/// This is <c>null</c> unless <see cref="FileLoggerOptions.IncludeScopes"/> is enabled, so that option remains
		/// the single opt-in for scope handling. It is also <c>null</c> if the logger provider was never given a scope
		/// provider by a logging factory.
		/// </remarks>
		public readonly IExternalScopeProvider ScopeProvider;

		internal LogMessage(string logName, LogLevel level, EventId eventId, string message, Exception ex,
				IExternalScopeProvider scopeProvider = null) {
			LogName = logName;
			Message = message;
			LogLevel = level;
			EventId = eventId;
			Exception = ex;
			ScopeProvider = scopeProvider;
		}

	}
}
