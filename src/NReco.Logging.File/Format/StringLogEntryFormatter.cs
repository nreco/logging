using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("NReco.Logging.Tests")]

namespace NReco.Logging.File.Format {

	/// <summary>
	/// Implements low-allocation default log entry formatting
	/// </summary>
	public class StringLogEntryFormatter {

		internal static readonly StringLogEntryFormatter Instance = new StringLogEntryFormatter();

		public StringLogEntryFormatter() {
		}

		string GetShortLogLevel(LogLevel logLevel) {
			return logLevel switch {
				LogLevel.Trace => "TRCE",
				LogLevel.Debug => "DBUG",
				LogLevel.Information => "INFO",
				LogLevel.Warning => "WARN",
				LogLevel.Error => "FAIL",
				LogLevel.Critical => "CRIT",
				LogLevel.None => "NONE",
				_ => logLevel.ToString().ToUpper(),
			};
		}

		/// <summary>
		/// This is a StringBuilder-based tab-separated log entry formatter (reference implementation).
		/// </summary>
		public string StringBuilderLogEntryFormat(string logName, DateTime timeStamp, LogLevel logLevel, EventId eventId, string message, Exception exception) {
			// default formatting logic
			var logBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(message)) {
				logBuilder.Append(timeStamp.ToString("o"));
				logBuilder.Append('\t');
				logBuilder.Append(GetShortLogLevel(logLevel));
				logBuilder.Append("\t[");
				logBuilder.Append(logName);
				logBuilder.Append("]");
				logBuilder.Append("\t[");
				logBuilder.Append(eventId);
				logBuilder.Append("]\t");
				logBuilder.Append(message);
			}

			if (exception != null) {
				// exception message
				logBuilder.AppendLine(exception.ToString());
			}
			return logBuilder.ToString();
		}

		public string StringBuilderLogEntryFormat(LogMessage logMsg, DateTime timeStamp)
			=> StringBuilderLogEntryFormat(logMsg.LogName, timeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

		/// <summary>
		/// This is a low-allocation optimized tab-separated log entry formatter that formats output identical to <see cref="StringBuilderLogEntryFormat"/>
		/// </summary>
		public string LowAllocLogEntryFormat(string logName, DateTime timeStamp, LogLevel logLevel, EventId eventId, string message, Exception exception) {
			const int MaxStackAllocatedBufferLength = 256;
			var logMessageLength = CalculateLogMessageLength();
			char[] charBuffer = null;
			try {
				Span<char> buffer = logMessageLength <= MaxStackAllocatedBufferLength
					? stackalloc char[MaxStackAllocatedBufferLength]
					: (charBuffer = ArrayPool<char>.Shared.Rent(logMessageLength));

				// default formatting logic
				using var logBuilder = new ValueStringBuilder(buffer);
				if (!string.IsNullOrEmpty(message)) {
					timeStamp.TryFormatO(logBuilder.RemainingRawChars, out var charsWritten);
					logBuilder.AppendSpan(charsWritten);
					logBuilder.Append('\t');
					logBuilder.Append(GetShortLogLevel(logLevel));
					logBuilder.Append("\t[");
					logBuilder.Append(logName);
					logBuilder.Append("]\t[");
					if (eventId.Name is not null) {
						logBuilder.Append(eventId.Name);
					}
					else {
						eventId.Id.TryFormat(logBuilder.RemainingRawChars, out charsWritten);
						logBuilder.AppendSpan(charsWritten);
					}
					logBuilder.Append("]\t");
					logBuilder.Append(message);
				}

				if (exception != null) {
					// exception message
					logBuilder.Append(exception.ToString());
					logBuilder.Append(Environment.NewLine);
				}
				return logBuilder.ToString();
			} finally {
				if (charBuffer is not null) {
					ArrayPool<char>.Shared.Return(charBuffer);
				}
			}

			int CalculateLogMessageLength() {
				return timeStamp.GetFormattedLength()
					+ 1 /* '\t' */
					+ 4 /* GetShortLogLevel */
					+ 2 /* "\t[" */
					+ (logName?.Length ?? 0)
					+ 3 /* "]\t[" */
					+ (eventId.Name?.Length ?? eventId.Id.GetFormattedLength())
					+ 2 /* "]\t" */
					+ (message?.Length ?? 0);
			}
		}

		public string LowAllocLogEntryFormat(LogMessage logMsg, DateTime timeStamp)
			=> LowAllocLogEntryFormat(logMsg.LogName, timeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

	}

	internal static class DateTimeExtensions {
		internal static int GetFormattedLength(this DateTime dateTime) {
			const int BaseCharCountInFormatO = 27;

			return BaseCharCountInFormatO + dateTime.Kind switch {
				DateTimeKind.Local => 6,
				DateTimeKind.Utc => 1,
				_ => 0
			};
		}

#if NETSTANDARD2_0
		internal static bool TryFormatO(this DateTime dateTime, Span<char> destination, out int charsWritten) {
			var charsRequired = GetFormattedLength(dateTime);

			if (destination.Length < charsRequired) {
				charsWritten = 0;
				return false;
			}

			charsWritten = charsRequired;

			var year = (uint)dateTime.Year;
			var month = (uint)dateTime.Month;
			var day = (uint)dateTime.Day;
			var hour = (uint)dateTime.Hour;
			var minute = (uint)dateTime.Minute;
			var second = (uint)dateTime.Second;
			var tick = (uint)(dateTime.Ticks - (dateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond));

			year.WriteDigits(destination, 4);
			destination[4] = '-';
			month.WriteDigits(destination.Slice(5), 2);
			destination[7] = '-';
			day.WriteDigits(destination.Slice(8), 2);
			destination[10] = 'T';
			hour.WriteDigits(destination.Slice(11), 2);
			destination[13] = ':';
			minute.WriteDigits(destination.Slice(14), 2);
			destination[16] = ':';
			second.WriteDigits(destination.Slice(17), 2);
			destination[19] = '.';
			tick.WriteDigits(destination.Slice(20), 7);

			var kind = dateTime.Kind;
			if (kind == DateTimeKind.Local) {
				var offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
				var offsetTotalMinutes = (int)(offset.Ticks / TimeSpan.TicksPerMinute);

				var sign = '+';
				if (offsetTotalMinutes < 0) {
					sign = '-';
					offsetTotalMinutes = -offsetTotalMinutes;
				}

				var offsetHours = Math.DivRem(offsetTotalMinutes, 60, out var offsetMinutes);

				destination[27] = sign;
				((uint)offsetHours).WriteDigits(destination.Slice(28), 2);
				destination[30] = ':';
				((uint)offsetMinutes).WriteDigits(destination.Slice(31), 2);
			}
			else if (kind == DateTimeKind.Utc) {
				destination[27] = 'Z';
			}

			return true;
		}
#else
		internal static bool TryFormatO(this DateTime dateTime, Span<char> destination, out int charsWritten) {
			return dateTime.TryFormat(destination, out charsWritten, format: "O");
		}
#endif

	}

	internal static class IntExtensions {

		/// <summary>
		/// This is a compute-optimized function that returns a number of decimal digets of the specified int value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		internal static int GetFormattedLength(this int value) {
			if (value == 0)
				return 1; // fast result for typical EventId value (0)
			uint absVal;
			int signLen = 0;
			if (value < 0) {
				absVal = ((uint)(~value)) + 1; // abs value of two's comlpement signed integer
				signLen = 1;
			}
			else {
				absVal = (uint)value;
			}
			if (absVal < 10)
				return 1 + signLen;
			else if (absVal < 100)
				return 2 + signLen;
			else if (absVal < 1000)
				return 3 + signLen;
			else if (absVal < 10000)
				return 4 + signLen;
			else if (absVal < 100000)
				return 5 + signLen;
			else if (absVal < 1000000)
				return 6 + signLen;
			else if (absVal < 10000000)
				return 7 + signLen;
			else if (absVal < 100000000)
				return 8 + signLen;
			else if (absVal < 1000000000)
				return 9 + signLen;
			else
				return 10 + signLen;
		}


#if NETSTANDARD2_0

		internal static bool TryFormat(this int value, Span<char> destination, out int charsWritten) {
			charsWritten = GetFormattedLength(value);
			if (destination.Length < charsWritten) {
				charsWritten = 0;
				return false;
			}

			var dst = destination.Slice(0, charsWritten);
			uint absVal;
			if (value < 0) {
				dst[0] = '-';
				dst = dst.Slice(1);
				absVal = ((uint)(~value)) + 1; // abs value of two's comlpement signed integer
			}
			else {
				absVal = (uint)value;
			}

			absVal.WriteDigits(dst, dst.Length);
			return true;


		}

		internal static void WriteDigits(this uint value, Span<char> destination, int count) {
			for (var cur = count - 1; cur > 0; cur--) {
				uint temp = '0' + value;
				value /= 10;
				destination[cur] = (char)(temp - (value * 10));
			}

			destination[0] = (char)('0' + value);
		}
#endif

	}

}
