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
	/// Opens write-only file streams that always append at the current end of the file.
	/// </summary>
	internal interface IAppendingFileStreamFactory {

		/// <summary>
		/// Opens (or creates) the specified file for appending.
		/// </summary>
		FileStream Open(string fileName, FileShare share);
	}

}
