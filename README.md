# NReco.Logging.File
Simple and efficient file logger provider for .NET Core (any version) without additional dependencies.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.Logging.File.svg)](https://www.nuget.org/packages/NReco.Logging.File/)

* very similar to standard ConsoleLogger but writes to a file
* can append to existing file or overwrite log file on restart
* supports a 'rolling file' behaviour and can control total log size
* it is possible to change log file name on-the-fly
* suitable for intensive concurrent usage: has internal message queue to avoid threads blocking

## How to use
Add *NReco.Logging.File* package reference and initialize a file logging provider in `services.AddLogging` (Startup.cs):
```
services.AddLogging(loggingBuilder => {
	loggingBuilder.AddFile("app.log", append:true);
});
```
or
```
services.AddLogging(loggingBuilder => {
	var loggingSection = Configuration.GetSection("Logging");
	loggingBuilder.AddFile(loggingSection);
});
```
Example of the configuration section in appsettings.json:
```
"Logging": {
	"LogLevel": {
	  "Default": "Debug",
	  "System": "Information",
	  "Microsoft": "Error"
	},
	"File": {
		"Path": "app.log",
		"Append": true,
		"MinLevel": "Warning",  // min level for the file logger
		"FileSizeLimitBytes": 0,  // use to activate rolling file behaviour
		"MaxRollingFiles": 0  // use to specify max number of log files
	}
}
```

## Rolling File
This feature is activated with `FileLoggerOptions` properties: `FileSizeLimitBytes` and `MaxRollingFiles`. Lets assume that file logger is configured for "test.log":

* if only `FileSizeLimitBytes` is specified file logger will create "test.log", "test1.log", "test2.log" etc
* use `MaxRollingFiles` in addition to `FileSizeLimitBytes` to limit number of log files; for example, for value "3" file logger will create "test.log", "test1.log", "test2.log" and again "test.log", "test1.log" (old files will be overwritten).
* if file name is changed in time (with `FormatLogFileName` handler) max number of files works only for the same file name. For example, if file name is based on date, `MaxRollingFiles` will limit number of log files only for the concrete date.

## Change log file name on-the-fly
It is possible to specify a custom log file name formatter with `FileLoggerOptions` property `FormatLogFileName`. Log file name may change in time - for example, to create a new log file per day:
```
services.AddLogging(loggingBuilder => {
	loggingBuilder.AddFile("app_{0:yyyy}-{0:MM}-{0:dd}.log", fileLoggerOpts => {
		fileLoggerOpts.FormatLogFileName = fName => {
			return String.Format(fName, DateTime.UtcNow);
		};
	});
});
```
Note that this handler is called on _every_ log message 'write'; you may cache the log file name calculation in your handler to avoid any potential overhead in case of high-load logger usage.

## Custom log entry formatting
You can specify `FileLoggerProvider.FormatLogEntry` handler to customize log entry content. For example, it is possible to write log entry as JSON array:
```
loggerFactory.AddProvider(new NReco.Logging.File.FileLoggerProvider("logs/app.js", true) {
	FormatLogEntry = (msg) => {
		var sb = new System.Text.StringBuilder();
		StringWriter sw = new StringWriter(sb);
		var jsonWriter = new Newtonsoft.Json.JsonTextWriter(sw);
		jsonWriter.WriteStartArray();
		jsonWriter.WriteValue(DateTime.Now.ToString("o"));
		jsonWriter.WriteValue(msg.LogLevel.ToString());
		jsonWriter.WriteValue(msg.LogName);
		jsonWriter.WriteValue(msg.EventId.Id);
		jsonWriter.WriteValue(msg.Message);
		jsonWriter.WriteValue(msg.Exception?.ToString());
		jsonWriter.WriteEndArray();
		return sb.ToString();
	}
});
```
(in case of .NET Core 2 use `loggingBuilder.AddProvider` instead of `loggerFactory.AddProvider`).

## License
Copyright 2017-2020 Vitaliy Fedorchenko and contributors

Distributed under the MIT license
