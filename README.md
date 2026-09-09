# NReco.Logging.File
Simple and efficient file logger provider for .NET Core (NET6+, NET10, NET11) without any additional dependencies.

NuGet | Tests
--- | --- 
[![NuGet Release](https://img.shields.io/nuget/v/NReco.Logging.File.svg)](https://www.nuget.org/packages/NReco.Logging.File/) | ![Tests](https://github.com/nreco/logging/actions/workflows/dotnet-test.yml/badge.svg) 

* very similar to standard ConsoleLogger but writes to a file
* can append to existing file or overwrite log file on restart
* supports a 'rolling file' behaviour and can control total log size
* it is possible to change log file name on-the-fly
* suitable for intensive concurrent usage: has internal message queue to avoid threads blocking
* optimized low-allocation default log messages formatting suitable for intensive usage

## How to use
Add *NReco.Logging.File* package reference and initialize a file logging provider in `services.AddLogging` (Startup.cs):
```csharp
using NReco.Logging.File;

services.AddLogging(loggingBuilder => {
	loggingBuilder.AddFile("app.log", append:true);
});
```
or
```csharp
services.AddLogging(loggingBuilder => {
	var loggingSection = Configuration.GetSection("Logging");
	loggingBuilder.AddFile(loggingSection);
});
```
Example of the configuration section in appsettings.json:
```jsonc
"Logging": {
	"LogLevel": {
	  "Default": "Debug",
	  "System": "Information",
	  "Microsoft": "Error"
	},
	"File": {
		"Path": "app.log",
		"Append": true,
		"ShareWriteAccess": false,  // set to true to let other processes write to the log file
		"ShareDeleteAccess": false,  // set to true to let other processes delete or move the log file
		"MinLevel": "Warning",  // min level for the file logger
		"IncludeScopes": false,  // set to true to include logging scopes
		"FileSizeLimitBytes": 0,  // use to activate rolling file behaviour
		"MaxRollingFiles": 0  // use to specify max number of log files
	}
}
```

## Logging scopes
Logging scopes can be included in the default log entry format by setting `FileLoggerOptions.IncludeScopes` to `true` (the default is `false`):
```csharp
loggingBuilder.AddFile("app.log", fileLoggerOpts => {
	fileLoggerOpts.IncludeScopes = true;
});
```
Active scopes are written from outermost to innermost before the message, for example: `=> Request 123 => Processing order`.
A custom `FormatLogEntry` handler replaces this format entirely and can render scopes itself with `LogMessage.ScopeProvider`, which is also gated by `IncludeScopes`.

## Rolling File
This feature is activated with `FileLoggerOptions` properties: `FileSizeLimitBytes` and `MaxRollingFiles`. Lets assume that file logger is configured for "test.log":

* if only `FileSizeLimitBytes` is specified file logger will create "test.log", "test1.log", "test2.log" etc
* use `MaxRollingFiles` in addition to `FileSizeLimitBytes` to limit number of log files; for example, for value "3" file logger will create "test.log", "test1.log", "test2.log" and again "test.log", "test1.log" (old files will be overwritten).
* if file name is changed in time (with `FormatLogFileName` handler) max number of files works only for the same file name. For example, if file name is based on date, `MaxRollingFiles` will limit number of log files only for the concrete date.

## Change log file name on-the-fly
It is possible to specify a custom log file name formatter with `FileLoggerOptions` property `FormatLogFileName`. Log file name may change in time - for example, to create a new log file per day:
```csharp
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
You can specify `FileLoggerOptions.FormatLogEntry` handler to customize log entry content. For example, it is possible to write log entry as JSON array:
```csharp
loggingBuilder.AddFile("logs/app.js", fileLoggerOpts => {
	fileLoggerOpts.FormatLogEntry = (msg) => {
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

## Custom log entry filtering
You may provide a predicate to perform filter log entries filtering on the logging provider level. This may be useful if you want to have 2 (or more)
file loggers that separate log entries between log files on some criteria:
```csharp
loggingBuilder.AddFile("logs/errors_only.log", fileLoggerOpts => {
	fileLoggerOpts.FilterLogEntry = (msg) => {
		return msg.LogLevel == LogLevel.Error;
	}
});
```

## File errors handling
Log file is opened immediately when `FileLoggerProvider` is created (= on `AddFile` call) and you may handle initial file opening errors simply by wrapping `AddFile` with a `try .. catch`. 
However you might want to propose a new log file name to guartee that file logging works even if an original log file is not accessible. To provide your own handling of file errors you may specify `HandleFileError` delegate:
```csharp
loggingBuilder.AddFile(loggingSection, fileLoggerOpts => {
	fileLoggerOpts.HandleFileError = (err) => {
		err.UseNewLogFileName( Path.GetFileNameWithoutExtension(err.LogFileName)+ "_alt" + Path.GetExtension(err.LogFileName) );
	};
});
```
Real-life implementation may be more complicated to guarantee that a new file name can be used without errors; note that `HandleFileError` is not recursive and it will not be called if proposed file name cannot be opened too. 

A new file name is applied in the same way as when it comes from the initial `FileLoggerProvider` options (if `FormatLogFileName` is specified it is called to resolve a final log file name).


## Sharing the log file with other processes
By default the log file is opened for exclusive writing: other processes may only read it. This can be relaxed with two `FileLoggerOptions` properties:

```csharp
loggingBuilder.AddFile("app.log", fileLoggerOpts => {
	fileLoggerOpts.ShareWriteAccess = true;
	fileLoggerOpts.ShareDeleteAccess = true;
});
```

* `ShareWriteAccess` (default `false`) allows other processes to write to the same log file. When it is enabled the file logger switches to an append-only file handle so that its own writes cannot overwrite entries that were appended by someone else in the meantime.
* `ShareDeleteAccess` (default `false`) allows other processes to delete or move the log file, which is what external log rotation tools usually do. When it is enabled the file logger checks before every write whether the current log file still exists and re-creates it if it does not.

## Platform-specific behavior
The options above rely on file sharing semantics that are not identical on all platforms:

* On Windows `FileShare` is enforced by the operating system, so a second writer really is rejected unless `ShareWriteAccess` is enabled. Because `FileMode.Append` on Windows only seeks to the end of the file once, `ShareWriteAccess` opens the file through a win32 handle with `FILE_APPEND_DATA` access, which makes the kernel append at the current end of the file on every write.
* On Linux and macOS `FileShare` is only advisory and is enforced between file streams of the same process, not across processes. Concurrent writers are therefore possible regardless of `ShareWriteAccess`, and a log file can always be deleted or moved by another process. `ShareWriteAccess` still matters there: it opens the file with `FileMode.Append` (the `O_APPEND` open flag), which guarantees atomic appends, and `ShareDeleteAccess` is still needed to make the logger re-create a log file that was removed externally.

## License
Copyright 2017-2026 Vitaliy Fedorchenko and contributors

Distributed under the MIT license
