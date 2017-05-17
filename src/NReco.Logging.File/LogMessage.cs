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
using System.Text;
using Microsoft.Extensions.Logging;

namespace NReco.Logging.File
{
	public struct LogMessage {
		public readonly string Message;
		public readonly LogLevel LogLevel;
		public readonly EventId EventId;
		public readonly Exception Exception;

		internal LogMessage(LogLevel level, EventId eventId, string message, Exception ex) {
			Message = message;
			LogLevel = level;
			EventId = eventId;
			Exception = ex;
		}

	}
}
