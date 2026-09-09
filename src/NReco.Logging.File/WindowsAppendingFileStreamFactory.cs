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
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif
using Microsoft.Win32.SafeHandles;

namespace NReco.Logging.File {

	/// <summary>
	/// Opens appending file streams through a win32 file handle that holds FILE_APPEND_DATA access.
	/// </summary>
	/// <remarks>
	/// On windows <see cref="FileMode.Append"/> only seeks to the end of the file once, so concurrent writers may overwrite each other.
	/// A handle that holds FILE_APPEND_DATA without FILE_WRITE_DATA makes the kernel ignore the write offset and append atomically instead.
	/// </remarks>
#if NET5_0_OR_GREATER
	[SupportedOSPlatform("windows")]
#endif
	internal sealed class WindowsAppendingFileStreamFactory : IAppendingFileStreamFactory {

		private const uint FILE_APPEND_DATA = 0x0004;
		private const uint SYNCHRONIZE = 0x00100000;
		private const uint OPEN_ALWAYS = 4;
		private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
		static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

		public FileStream Open(string fileName, FileShare share) {
			// FileShare values match the win32 FILE_SHARE_* flags, but FileShare.Inheritable has no win32 counterpart and has to be removed first
			var shareMode = (uint)(share & ~FileShare.Inheritable);

			var handle = CreateFile(fileName, FILE_APPEND_DATA | SYNCHRONIZE, shareMode, IntPtr.Zero, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
			if (handle.IsInvalid) {
				var lastError = Marshal.GetLastWin32Error();
				handle.Dispose();
				throw new IOException($"Cannot open file '{fileName}'.", new Win32Exception(lastError));
			}
			return new FileStream(handle, FileAccess.Write);
		}
	}

}
