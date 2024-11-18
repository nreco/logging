using System;
using System.Linq;
using Xunit;
using NReco.Logging.File.Format;
using NReco.Logging.File;
using Microsoft.Extensions.Logging;

namespace NReco.Logging.Tests {
	public class StringLogEntryFormatterTests {

		[Theory]
		[InlineData("t1", LogLevel.Trace, 0, "Test message",null)]
		[InlineData("t1", LogLevel.Critical, 1, "Error", "Invalid operation")]
		[InlineData("t1", LogLevel.Warning, Int32.MaxValue, "Error", "Invalid operation")]
		[InlineData("t2", LogLevel.Debug, Int32.MinValue, "Error", "Invalid operation")]
		[InlineData("t2", LogLevel.Debug, -1, "M", null)]
		public void LowAllocLogEntryFormat(string logName, LogLevel lvl, int? evId, string msg, string exceptionMsg) {
			var formatter = StringLogEntryFormatter.Instance;
			var dt = DateTime.Now;
			Exception ex = null;
			try {
				if (exceptionMsg != null)
					throw new Exception(exceptionMsg);
			} catch (Exception exception) {
				ex = exception;
			}
			var logMsg = new LogMessage(logName, lvl, new EventId(evId.Value), msg, ex);
			Assert.Equal(
				formatter.StringBuilderLogEntryFormat(logMsg,dt),
				formatter.LowAllocLogEntryFormat(logMsg, dt));
		}

		[Fact]
		public void LowAllocLogEntryFormatVeryLongMessage() {
			LowAllocLogEntryFormat("aaa", LogLevel.Trace, 0, String.Concat(Enumerable.Repeat("TestValue ", 1000)), "test");
		}


		[Fact]
		public void GetFormattedLengthOfZero() {
			var result = 0.GetFormattedLength();

			Assert.Equal(1, result);
		}

		[Fact]
		public void GetFormattedIntLengthOfMinValue() {
			var result = int.MinValue.GetFormattedLength();

			Assert.Equal(11, result);
		}

		[Fact]
		public void GetFormattedLengthOfMaxValue() {
			var result = int.MaxValue.GetFormattedLength();

			Assert.Equal(10, result);
		}

		[Fact]
		public void GetFormattedLength() {

			for (int i = Int32.MaxValue; i > 0; i = i / 10) {
				Assert.Equal(getFormattedLengthByFormula(i), i.GetFormattedLength());
			}

			for (int i = Int32.MinValue; i < 0; i = i / 10)
				Assert.Equal(getFormattedLengthByFormula(i), i.GetFormattedLength());

			int getFormattedLengthByFormula(int v) {
				return v == 0 ? 1 : (int)Math.Floor(Math.Log10(Math.Abs((double)v))) + (v > 0 ? 1 : 2);
			}
		}

		[Fact]
		public void IntTryFormatZero() {
			const string expected = "0\0\0\0\0\0\0\0\0\0\0";

			Span<char> span = stackalloc char[11];
			var result = 0.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(1, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void IntTryFormatMinValue() {
			const string expected = "-2147483648";

			Span<char> span = stackalloc char[11];
			var result = int.MinValue.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(11, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void IntTryFormatMaxValue() {
			const string expected = "2147483647\0";

			Span<char> span = stackalloc char[11];
			var result = int.MaxValue.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(10, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void IntTryFormatTooShortSpan() {
			const string expected = "\0\0\0\0\0";

			Span<char> span = stackalloc char[5];
			var result = int.MaxValue.TryFormat(span, out var charsWritten);

			Assert.False(result);
			Assert.Equal(0, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void GetFormattedLengthOfDateTimeNow() {
			var result = DateTime.Now.GetFormattedLength();

			Assert.Equal(33, result);
		}

		[Fact]
		public void GetFormattedLengthOfDateTimeUtcNow() {
			var result = DateTime.UtcNow.GetFormattedLength();

			Assert.Equal(28, result);
		}

		[Fact]
		public void GetFormattedLengthOfDateTimeKindUnspecified() {
			var result = new DateTime(2024, 01, 01, 01, 01, 01, DateTimeKind.Unspecified).GetFormattedLength();

			Assert.Equal(27, result);
		}

		[Fact]
		public void GetFormattedLengthOfDefaultDateTime() {
			var result = default(DateTime).GetFormattedLength();

			Assert.Equal(27, result);
		}

		[Fact]
		public void DateTimeTryFormatLocal() {
			var testValue = DateTime.Now;
			var expected = $"{testValue:O}";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(33, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void DateTimeTryFormatUtc() {
			var testValue = DateTime.UtcNow;
			var expected = $"{testValue:O}\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(28, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void DateTimeTryFormatUnspecified() {
			var testValue = new DateTime(2024, 1, 1, 1, 1, 1, DateTimeKind.Unspecified);
			var expected = $"{testValue:O}\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void DateTimeTryFormatDefault() {
			var testValue = default(DateTime);
			var expected = $"{testValue:O}\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void DateTimeTryFormatMaxValue() {
			var testValue = DateTime.MaxValue;
			var expected = $"{testValue:O}\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void DateTimeTryFormatIntoTooShortSpan() {
			var testValue = DateTime.Now;
			ReadOnlySpan<char> expected = stackalloc char[25];

			Span<char> span = stackalloc char[25];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.False(result);
			Assert.Equal(0, charsWritten);
			Assert.True(span.SequenceEqual(expected));
		}


	}
}
