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

using System.IO;

namespace NReco.Logging.File {

	/// <summary>
	/// Opens appending file streams with <see cref="FileMode.Append"/>.
	/// </summary>
	/// <remarks>
	/// On unix-like systems <see cref="FileMode.Append"/> is translated into the O_APPEND open flag which makes every write atomic
	/// with respect to the end of the file, so no platform-specific handling is needed there.
	/// </remarks>
	internal sealed class GenericAppendingFileStreamFactory : IAppendingFileStreamFactory {

		public FileStream Open(string fileName, FileShare share) {
			return new FileStream(fileName, FileMode.Append, FileAccess.Write, share);
		}
	}

}
