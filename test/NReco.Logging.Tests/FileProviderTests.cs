using System;
using System.Collections.Generic;
using Xunit;
using NReco.Logging.File;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IO;

namespace NReco.Logging.Tests
{

	public class FileProviderTests {

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

	}
}
