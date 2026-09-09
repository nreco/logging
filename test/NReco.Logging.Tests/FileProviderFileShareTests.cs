using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using Xunit;

namespace NReco.Logging.Tests
{

	public class FileProviderFileShareTests {

		[Fact]
		public void ShareWriteAccessAllowsAnotherWriter() {
			var tmpFile = Path.GetTempFileName();
			try {
				using (var provider = new FileLoggerProvider(tmpFile, new FileLoggerOptions() { ShareWriteAccess = true })) {
					provider.CreateLogger("TEST").LogInformation("Line1");

					using (var otherWriter = new FileStream(tmpFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) {
						Assert.True(otherWriter.CanWrite);
					}
				}
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void WithoutShareWriteAccessAnotherWriterIsBlocked() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return; // FileShare is not enforced across processes on unix-like systems

			var tmpFile = Path.GetTempFileName();
			try {
				using (var provider = new FileLoggerProvider(tmpFile)) {
					provider.CreateLogger("TEST").LogInformation("Line1");

					Assert.Throws<IOException>(() => new FileStream(tmpFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
				}
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void ShareDeleteAccessRecreatesDeletedLogFile() {
			var tmpFile = Path.GetTempFileName();
			try {
				using (var provider = new FileLoggerProvider(tmpFile, new FileLoggerOptions() { ShareWriteAccess = true, ShareDeleteAccess = true })) {
					var logger = provider.CreateLogger("TEST");
					logger.LogInformation("Line1");
					WaitForFlush(tmpFile);

					System.IO.File.Delete(tmpFile);
					logger.LogInformation("Line2");
				}

				Assert.Equal(1, System.IO.File.ReadAllLines(tmpFile).Length);
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void ShareWriteAccessWithoutAppendTruncatesLogFile() {
			var tmpFile = Path.GetTempFileName();
			try {
				System.IO.File.WriteAllText(tmpFile, $"Existing1{Environment.NewLine}Existing2{Environment.NewLine}");

				using (var provider = new FileLoggerProvider(tmpFile, new FileLoggerOptions() { Append = false, ShareWriteAccess = true })) {
					provider.CreateLogger("TEST").LogInformation("Line1");
				}

				Assert.Equal(1, System.IO.File.ReadAllLines(tmpFile).Length);
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		// external modifications of the log file must not affect the entries that are written afterwards

		[Fact]
		public void ForeignAppendAtEndDoesNotAffectLogger() {
			AssertLoggerAppendsAfterForeignChange(
				foreignChange: logFile => {
					using (var writer = OpenForeignWriter(logFile)) {
						writer.Seek(0, SeekOrigin.End);
						WriteLine(writer, "Foreign");
					}
				},
				expectedLines: new[] { "Line1", "Foreign", "Line2" });
		}

		[Fact]
		public void ForeignOverwriteAtBeginningDoesNotAffectLogger() {
			AssertLoggerAppendsAfterForeignChange(
				foreignChange: logFile => {
					using (var writer = OpenForeignWriter(logFile)) {
						writer.Seek(0, SeekOrigin.Begin);
						Write(writer, "Fore1"); // same length as "Line1", so the entry is replaced in place
					}
				},
				expectedLines: new[] { "Fore1", "Line2" });
		}

		[Fact]
		public void ForeignInsertInMiddleDoesNotAffectLogger() {
			AssertLoggerAppendsAfterForeignChange(
				foreignChange: logFile => {
					using (var writer = OpenForeignWriter(logFile)) {
						writer.Seek(2, SeekOrigin.Begin);
						Write(writer, "XY");
					}
				},
				expectedLines: new[] { "LiXY1", "Line2" });
		}

		[Fact]
		public void ForeignTruncationDoesNotAffectLogger() {
			// a truncating writer moves the end of the file backwards: an append-only handle has to follow it instead of writing into a gap
			AssertLoggerAppendsAfterForeignChange(
				foreignChange: logFile => {
					using (var writer = OpenForeignWriter(logFile)) {
						writer.SetLength(0);
					}
				},
				expectedLines: new[] { "Line2" });
		}

		[Fact]
		public void TwoProvidersWithShareWriteAccessKeepAllEntries() {
			var tmpFile = Path.GetTempFileName();
			try {
				using (var firstProvider = CreateProvider(tmpFile))
				using (var secondProvider = CreateProvider(tmpFile, append: true)) {
					firstProvider.CreateLogger("TEST").LogInformation("First");
					secondProvider.CreateLogger("TEST").LogInformation("Second");

					WaitForLines(tmpFile, 2);
				}

				var logEntries = ReadAllSharedLines(tmpFile);
				Assert.Equal(2, logEntries.Length);
				Assert.Contains("First", logEntries);
				Assert.Contains("Second", logEntries);
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		static void AssertLoggerAppendsAfterForeignChange(Action<string> foreignChange, string[] expectedLines) {
			var tmpFile = Path.GetTempFileName();
			try {
				using (var provider = CreateProvider(tmpFile)) {
					var logger = provider.CreateLogger("TEST");
					logger.LogInformation("Line1");
					WaitForLines(tmpFile, 1);

					foreignChange(tmpFile);

					logger.LogInformation("Line2");
					WaitForLines(tmpFile, expectedLines.Length);
				}

				Assert.Equal(expectedLines, ReadAllSharedLines(tmpFile));
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		// the plain message is used as log entry so that the expected file content stays readable
		static FileLoggerProvider CreateProvider(string logFileName, bool append = false) {
			return new FileLoggerProvider(logFileName, new FileLoggerOptions() {
				Append = append,
				ShareWriteAccess = true,
				FormatLogEntry = logMessage => logMessage.Message
			});
		}

		static FileStream OpenForeignWriter(string logFileName) {
			return new FileStream(logFileName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
		}

		static void Write(FileStream stream, string text) {
			var bytes = System.Text.Encoding.UTF8.GetBytes(text);
			stream.Write(bytes, 0, bytes.Length);
		}

		static void WriteLine(FileStream stream, string text) {
			Write(stream, text + Environment.NewLine);
		}

		static void WaitForLines(string logFileName, int expectedLineCount) {
			for (var i = 0; i < 100 && ReadAllSharedLines(logFileName).Length < expectedLineCount; i++)
				System.Threading.Thread.Sleep(10);
		}

		static string[] ReadAllSharedLines(string logFileName) {
			var lines = new System.Collections.Generic.List<string>();
			using (var stream = new FileStream(logFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			using (var reader = new StreamReader(stream)) {
				string line;
				while ((line = reader.ReadLine()) != null)
					lines.Add(line);
			}
			return lines.ToArray();
		}

		static void WaitForFlush(string logFileName) {
			for (var i = 0; i < 100 && new FileInfo(logFileName).Length == 0; i++)
				System.Threading.Thread.Sleep(10);
		}
	}
}
