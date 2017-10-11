# NReco.Logging.File
Simple file logger provider for .NET Core (.NET Core 2) without additional dependencies.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.Logging.File.svg)](https://www.nuget.org/packages/NReco.Logging.File/)

* very similar to standard ConsoleLogger but writes to a file
* can append to a file
* suitable for intensive concurrent usage: has internal message queue to avoid threads blocking

## How to use
Add *NReco.Logging.File* package reference and initialize file logging provider:

.NET Core 2 | .NET Core 1.x
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
		"Append": "True"
	}
}
```

## Custom log entry formatting
You can specify `FileLoggerProvider.FormatLogEntry` handler to customize log entry content. For example, it is possible to write log entry as JSON array:
```
loggerFactory.AddProvider(new NReco.Logging.File.FileLoggerProvider("App_Data/stweb.log", true) {
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
(in case of .NET Core use `loggingBuilder.AddProvider` instead).

## License
Copyright 2017 Vitaliy Fedorchenko

Distributed under the MIT license