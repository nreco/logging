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

using System.Runtime.InteropServices;

namespace NReco.Logging.File {

	/// <summary>
	/// Resolves the <see cref="IAppendingFileStreamFactory"/> implementation that matches the current platform.
	/// </summary>
	internal static class AppendingFileStreamFactory {

		// resolved once per process: the platform cannot change at runtime
		static readonly IAppendingFileStreamFactory CurrentPlatformFactory =
			RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? new WindowsAppendingFileStreamFactory()
				: new GenericAppendingFileStreamFactory();

		internal static IAppendingFileStreamFactory CreateForCurrentPlatform() => CurrentPlatformFactory;
	}

}
