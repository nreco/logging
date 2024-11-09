using System;

#nullable enable

namespace NReco.Logging.File.Extensions {
	public static class DateTimeExtensions {
		public static bool TryFormatO(this DateTime dateTime, Span<char> destination, out int charsWritten) {
			const int BaseCharCountInFormatO = 27;

			int charsRequired = BaseCharCountInFormatO;
			var kind = dateTime.Kind;
			var offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
			if (kind == DateTimeKind.Local) {
				charsRequired += 6;
			}
			else if (kind == DateTimeKind.Utc) {
				charsRequired++;
			}

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

			if (kind == DateTimeKind.Local) {
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
	}
}
