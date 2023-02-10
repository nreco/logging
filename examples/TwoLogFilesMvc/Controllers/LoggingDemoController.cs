using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;


namespace TwoLogFilesMvc {
	
	public class LoggingDemoController : Controller {

		ILogger<LoggingDemoController> Logger;

		public LoggingDemoController(ILogger<LoggingDemoController> logger) {
			Logger = logger;
		}

		public IActionResult DemoPage() {
			return View();
		}

		public IActionResult LogMessage(string logLevel, string msg) {
			Logger.Log( (LogLevel)Enum.Parse(typeof(LogLevel), logLevel, true), msg);
			return Ok();
		}

	}
}
