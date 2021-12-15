using System;
using System.Collections.Generic;
using Xunit;
using NReco.Logging.File;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace NReco.Logging.Tests
{

	public class FileProviderTests {

		[Fact]
		public void WriteToFileAndOverwrite() {
			var tmpFile = Path.GetTempFileName();
			try {
				var factory = new LoggerFactory();
				factory.AddProvider(new FileLoggerProvider(tmpFile, false));
				var logger = factory.CreateLogger("TEST");
				logger.LogInformation("Line1");
				factory.Dispose();

				Assert.Equal(1, System.IO.File.ReadAllLines(tmpFile).Length);

				factory = new LoggerFactory();
				logger = factory.CreateLogger("TEST");
				factory.AddProvider(new FileLoggerProvider(tmpFile, false));
				logger.LogInformation("Line2");
				factory.Dispose();

				Assert.Equal(1, System.IO.File.ReadAllLines(tmpFile).Length);  // file should be overwritten

			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void WriteToFileAndAppend() {
			var tmpFile = Path.GetTempFileName();
			try {
				var factory = new LoggerFactory();
				factory.AddProvider(new FileLoggerProvider(tmpFile));

				var logger = factory.CreateLogger("TEST");
				logger.LogDebug("Debug message");

				logger.LogWarning("Warning message");

				factory.Dispose();

				//System.IO.File.WriteAllText("test.txt", System.IO.File.ReadAllText(tmpFile));
				var logEntries = System.IO.File.ReadAllLines(tmpFile);
				Assert.Equal(2, logEntries.Length);

				var entry1Parts = logEntries[0].Split('\t');
				Assert.Equal("[TEST]", entry1Parts[2]);
				Assert.Equal("Debug message", entry1Parts[4]);
				Assert.True(DateTime.Parse(entry1Parts[0]).Ticks <= DateTime.Now.Ticks);

				var entry2Parts = logEntries[1].Split('\t');
				Assert.Equal("[TEST]", entry2Parts[2]);
				Assert.Equal("Warning message", entry2Parts[4]);

				factory = new LoggerFactory();
				logger = factory.CreateLogger("TEST2");
				factory.AddProvider(new FileLoggerProvider(tmpFile, true));
				logger.LogInformation("Just message");
				factory.Dispose();

				Assert.Equal(3, System.IO.File.ReadAllLines(tmpFile).Length);
				
			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void WriteRollingFile() {
			var tmpFileDir = Path.GetTempFileName();  // for test debug: "./"
			System.IO.File.Delete(tmpFileDir);

			Directory.CreateDirectory(tmpFileDir);
			try {
				var logFile = Path.Combine(tmpFileDir, "test.log");

				LoggerFactory factory = null;
				ILogger logger = null;
				createFactoryAndTestLogger();

				for (int i = 0; i < 400; i++) {
					logger.LogInformation("TEST 0123456789");
					if (i % 50 == 0) {
						System.Threading.Thread.Sleep(20); // give some time for log writer to handle the queue
					}
				}
				factory.Dispose();

				// check how many files are created
				Assert.Equal(4, Directory.GetFiles(tmpFileDir, "test*.log").Length);
				var lastFileSize = new FileInfo(Path.Combine(tmpFileDir, "test3.log")).Length;

				// create new factory and continue
				createFactoryAndTestLogger();
				logger.LogInformation("TEST 0123456789");
				factory.Dispose();
				Assert.True(new FileInfo(Path.Combine(tmpFileDir, "test3.log")).Length > lastFileSize);

				// add many entries and ensure that there are only 5 log files
				createFactoryAndTestLogger();
				for (int i = 0; i < 1000; i++) {
					logger.LogInformation("TEST 0123456789");
				}
				factory.Dispose();
				Assert.Equal(5, Directory.GetFiles(tmpFileDir, "test*.log").Length);

				void createFactoryAndTestLogger() {
					factory = new LoggerFactory();
					factory.AddProvider(new FileLoggerProvider(logFile, new FileLoggerOptions() {
						FileSizeLimitBytes = 1024 * 8,
						MaxRollingFiles = 5
					}));
					logger = factory.CreateLogger("TEST");
				}

			} finally {
				Directory.Delete(tmpFileDir, true);
			}
		}


		/// <summary>
		/// As WriteRollingFile but checks for stability if some files are in use
		/// </summary>
		[Fact]
		public void StabilityWhenFilesInUse() {
			var tmpFileDir = Path.GetTempFileName();  // for test debug: "./"
			System.IO.File.Delete(tmpFileDir);

			Directory.CreateDirectory(tmpFileDir);
			LoggerFactory factory = null;
			ILogger logger = null;
			var logFile = Path.Combine(tmpFileDir, "test.log");
			try {


				// We lock files test.log and test1.log so they can't be touched by the logger
				using (FileStream fs_lockingFile0 = new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
				{
					using (FileStream fs_lockingFile1 = new FileStream(Path.Combine(tmpFileDir, "test1.log"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
					{
						// Create the factory and logger
						factory = new LoggerFactory();
						factory.AddProvider(new FileLoggerProvider(logFile, new FileLoggerOptions()
						{
							FileSizeLimitBytes = 1024 * 8,
							MaxRollingFiles = 5
						}));
						logger = factory.CreateLogger("TEST");


						// Write many logs 
						for (int i = 0; i < 1000; i++)
						{
							logger.LogInformation("TEST 0123456789");
						}
						System.Threading.Thread.Sleep(100); // give some time for log writer to handle the queue

						// check how many files are created
						// Note the first two files were created before the logger was created
						// so still must be counted but should be empty
						Assert.Equal(5, Directory.GetFiles(tmpFileDir, "test*.log").Length);

						Assert.Equal(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test.log")).Length);// locked
						Assert.Equal(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test1.log")).Length);// locked
						Assert.NotEqual(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test2.log")).Length);
						Assert.NotEqual(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test3.log")).Length);
						Assert.NotEqual(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test4.log")).Length);
					}
				}


				// Now the files are unlocked, continue logging and check the previously locked files are written to
				for (int i = 0; i < 400; i++)
				{
					logger.LogInformation("TEST 0123456789");
				}

				factory.Dispose();

				Assert.NotEqual(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test.log")).Length);
				Assert.NotEqual(0, new System.IO.FileInfo(Path.Combine(tmpFileDir, "test1.log")).Length);
			}
			finally {
				Directory.Delete(tmpFileDir, true);
			}
		}

		/// <summary>
		/// Checks an error is thrown if the file to write to is busy and skipping is disables
		/// </summary>
		[Fact]
		public void ThrowIfFilesInUse() {
			var tmpFileDir = Path.GetTempFileName();  // for test debug: "./"
			System.IO.File.Delete(tmpFileDir);

			Directory.CreateDirectory(tmpFileDir);
			LoggerFactory factory = null;
			var logFile = Path.Combine(tmpFileDir, "test.log");
			try {
				// We lock files test.log and test1.log so they can't be touched by the logger
				using (FileStream fs_lockingFile0 = new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
				{
					using (FileStream fs_lockingFile1 = new FileStream(Path.Combine(tmpFileDir, "test1.log"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
					{
						// Create the factory and logger
						using (factory = new LoggerFactory())
						{
							// Skip turned off and none available
							// The raw IOException should be thrown
							Assert.ThrowsAny<IOException>(() =>
								factory.AddProvider(new FileLoggerProvider(logFile, new FileLoggerOptions()
								{
									FileSizeLimitBytes = 1024 * 8,
									MaxRollingFiles = 1,
									SkipErroneousLogFiles = false
								}))
							);

							// Can skip but none available
							// An aggregate exception of all IOExceptions should be thrown
							Assert.ThrowsAny<AggregateException>(() =>
								factory.AddProvider(new FileLoggerProvider(logFile, new FileLoggerOptions()
								{
									FileSizeLimitBytes = 1024 * 8,
									MaxRollingFiles = 1,
									SkipErroneousLogFiles = true
								}))
								);

							// Skip turned off and many available
							// The raw IOException should be thrown
							Assert.ThrowsAny<IOException>(() =>
							factory.AddProvider(new FileLoggerProvider(logFile, new FileLoggerOptions()
							{
								FileSizeLimitBytes = 1024 * 8,
								MaxRollingFiles = 10,
								SkipErroneousLogFiles = false
							}))
							);
						}
					}
				}
			}
			finally {
				Directory.Delete(tmpFileDir, true);
			}
		}

		[Fact]
		public void WriteConcurrent() {
			var tmpFile = Path.GetTempFileName();
			try {
				var factory = new LoggerFactory();
				factory.AddProvider(new FileLoggerProvider(tmpFile));

				var writeTasks = new List<Task>();
				for (int i=0; i<5; i++) {
					writeTasks.Add(
						Task.Factory.StartNew(() => {
							var logger = factory.CreateLogger("TEST"+i.ToString());
							for (int j=0; j<100000; j++) {
								logger.LogInformation("MSG"+j.ToString());
							}
						})
					);

				}
				Task.WaitAll(writeTasks.ToArray());

				factory.Dispose();
				int lines = 0;
				using (var fileStream = new FileStream(tmpFile, FileMode.Open, FileAccess.Read)) {
					using (var rdr = new StreamReader(fileStream)) {
						while (true) {
							var s = rdr.ReadLine();
							if (s!=null) {
								lines++;
							} else
								break;
						}
					}
				}
				Assert.Equal(500000, lines);

			} finally {
				System.IO.File.Delete(tmpFile);
			}
		}

		[Fact]
		public void CreateDirectoryAutomatically()
		{
			
			var tmpFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "testfile.log");
			try
			{
				var factory = new LoggerFactory();
				
				factory.AddProvider(new FileLoggerProvider(tmpFile, new FileLoggerOptions()));
				
				var logger = factory.CreateLogger("TEST");
				logger.LogInformation("Line1");
				factory.Dispose();

				Assert.Single(System.IO.File.ReadAllLines(tmpFile));
			}
			finally
			{
				var directory = Path.GetDirectoryName(tmpFile);
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory, true);
				}
			}
		}

		[Fact]
		public void ExpandEnvironmentVariables()
		{
			var tmpFileWithEnvironmentVariable = "%TEMP%\\" + Path.GetFileName(Path.GetTempFileName());
			var expandedTmpFileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(tmpFileWithEnvironmentVariable));
			try
			{
				var factory = new LoggerFactory();
				factory.AddProvider(new FileLoggerProvider(tmpFileWithEnvironmentVariable, false));
				var logger = factory.CreateLogger("TEST");
				logger.LogInformation("Line1");
				factory.Dispose();

				Assert.Single(System.IO.File.ReadAllLines(expandedTmpFileName));
			}
			finally
			{
				System.IO.File.Delete(expandedTmpFileName);
			}

		}

		[Fact]
		public void CustomLogFileNameFormatter() {
			var tmpFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "testfile_{0}.log");

			try {
				var factory = new LoggerFactory();
				int fmtLogFileNameIdx = 0;

				factory.AddProvider(new FileLoggerProvider(tmpFile, new FileLoggerOptions() {
					FormatLogFileName = (fName) => {
						return String.Format(fName, fmtLogFileNameIdx);
					}
				}));

				var logger = factory.CreateLogger("TEST");
				for (int i = 0; i < 15; i++) {
					fmtLogFileNameIdx = i / 5;
					if (i > 0 && i % 5 == 0)
						Thread.Sleep(1000); // log writer works in another thread. Let him to process log messages in queue.
					logger.LogInformation("Line" + (i + 1).ToString());
					
				}
				factory.Dispose();

				// should be 3 files, each with 5 lines
				var logFiles = Directory.GetFiles( Path.GetDirectoryName(tmpFile) , "*.*", SearchOption.TopDirectoryOnly);
				Assert.Equal(3, logFiles.Length);
			} finally {
				var directory = Path.GetDirectoryName(tmpFile);
				if (Directory.Exists(directory)) {
					Directory.Delete(directory, true);
				}
			}

		}


	}
}
