using NReco.Logging.File.Extensions;
using System;
using Xunit;

namespace NReco.Logging.Tests {
	public class DateTimeExtensionsTests {

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
			var testValue = new DateTime(2024, 01, 01, 01, 01, 01, DateTimeKind.Unspecified);

			var result = testValue.GetFormattedLength();

			Assert.Equal(27, result);
		}

		[Fact]
		public void GetFormattedLengthOfDefaultDateTime() {
			var result = default(DateTime).GetFormattedLength();

			Assert.Equal(27, result);
		}

		[Fact]
		public void TryFormatLocal() {
			var testValue = DateTime.Now;
			var expected = testValue.ToString("O");

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(33, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatUtc() {
			var testValue = DateTime.UtcNow;
			var expected = testValue.ToString("O") + "\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(28, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatUnspecified() {
			var testValue = new DateTime(2024, 1, 1, 1, 1, 1, DateTimeKind.Unspecified);
			var expected = testValue.ToString("O") + "\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatDefault() {
			var testValue = default(DateTime);
			var expected = testValue.ToString("O") + "\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatMaxValue() {
			var testValue = DateTime.MaxValue;
			var expected = testValue.ToString("O") + "\0\0\0\0\0\0";

			Span<char> span = stackalloc char[33];
			var result = testValue.TryFormatO(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(27, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatIntoTooShortSpan() {
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
