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

## License
Copyright 2017 Vitaliy Fedorchenko

Distributed under the MIT license