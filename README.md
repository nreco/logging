# NReco.Logging.File
Simple and efficient file logger provider for .NET Core (any version) without additional dependencies.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.Logging.File.svg)](https://www.nuget.org/packages/NReco.Logging.File/)

* very similar to standard ConsoleLogger but writes to a file
* can append to a file
* supports rolling file behaviour and can control total log size
* suitable for intensive concurrent usage: has internal message queue to avoid threads blocking

## How to use
Add *NReco.Logging.File* package reference and initialize file logging provider:

.NET Core 2.x/3.x | .NET Core 1.x
----------- | -------------
In services.AddLogging | In Startup.cs Configure method
`loggingBuilder.AddFile("app.log", append:true);`<br/>or<br/>`var loggingSection = Configuration.GetSection("Logging");`<br/>`loggingBuilder.AddFile(loggingSection);` | `loggerFactory.AddFile("app.log", append:true);`<br/>or<br/>`var loggingSection = Configuration.GetSection("Logging");`<br/>`loggerFactory.AddFile(loggingSection);`

Example of configuration section from appsettings.json:
```
"Logging": {
	"LogLevel": {
	  "Default": "Debug",
	  "System": "Information",
	  "Microsoft": "Error"
	},
	"File": {
		"Path": "app.log",
		"Append": "True",
		"FileSizeLimitBytes": 0,  // use to activate rolling file behaviour
		"MaxRollingFiles": 0  // use to specify max number of log files
	}
}
```

## Rolling File
This feature is activated with `FileLoggerOptions` properties: `FileSizeLimitBytes` and `MaxRollingFiles`. Lets assume that file logger is configured for "test.log":

* if only `FileSizeLimitBytes` is specified file logger will create "test.log", "test1.log", "test2.log" etc
* use `MaxRollingFiles` in addition to `FileSizeLimitBytes` to limit number of log files; for example, for value "3" file logger will create "test.log", "test1.log", "test2.log" and again "test.log", "test1.log" (old files will be overwritten).

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