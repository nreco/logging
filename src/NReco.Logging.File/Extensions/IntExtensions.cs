using System;

#nullable enable

namespace NReco.Logging.File.Extensions {
	public static class IntExtensions {
		public static int GetFormattedLength(this int value) {
			return value == 0 ? 1 : (int)Math.Floor(Math.Log10(Math.Abs((double)value))) + (value > 0 ? 1 : 2);
		}

#if NETSTANDARD2_0
		public static bool TryFormat(this int value, Span<char> destination, out int charsWritten) {
			charsWritten = value.GetFormattedLength();
			if (destination.Length < charsWritten) {
				charsWritten = 0;
				return false;
			}

			var dst = destination.Slice(0, charsWritten);

			if (value < 0) {
				dst[0] = '-';
				dst = dst.Slice(1);
			}

			((uint)Math.Abs((long)value)).WriteDigits(dst, dst.Length);
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
