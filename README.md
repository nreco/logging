# NReco.Logging.File
Generic file logger provider for .NET Core with minimal dependencies.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.Logging.File.svg)](https://www.nuget.org/packages/NReco.Logging.File/)

* very similar to standard ConsoleLogger but writes to file
* suitable for intensive concurrent usage: has internal message queue to avoid threads blocking

## How to use
Add dependency on NReco.Logging.File and add to Startup.cs:

```
loggerFactory.AddFile("app.log", append:true);
```  
or initialize settings with section from appsettings.json:
```
var loggingConfig = Configuration.GetSection("Logging");
loggerFactory.AddFile(loggingConfig);
```  
in this case log file will use "LogLevel" section and "File" section:
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

## License
Copyright 2017 Vitaliy Fedorchenko

Distributed under the MIT license