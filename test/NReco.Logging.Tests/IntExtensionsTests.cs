using NReco.Logging.File.Extensions;
using System;
using Xunit;

namespace NReco.Logging.Tests {
	public class IntExtensionsTests {

		[Fact]
		public void GetFormattedLengthOfZero() {
			var result = 0.GetFormattedLength();

			Assert.Equal(1, result);
		}

		[Fact]
		public void GetFormattedLengthOfMinValue() {
			var result = int.MinValue.GetFormattedLength();

			Assert.Equal(11, result);
		}

		[Fact]
		public void GetFormattedLengthOfMaxValue() {
			var result = int.MaxValue.GetFormattedLength();

			Assert.Equal(10, result);
		}

		[Fact]
		public void TryFormatZero() {
			const string expected = "0\0\0\0\0\0\0\0\0\0\0";

			Span<char> span = stackalloc char[11];
			var result = 0.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(1, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatMinValue() {
			const string expected = "-2147483648";

			Span<char> span = stackalloc char[11];
			var result = int.MinValue.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(11, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatMaxValue() {
			const string expected = "2147483647\0";

			Span<char> span = stackalloc char[11];
			var result = int.MaxValue.TryFormat(span, out var charsWritten);

			Assert.True(result);
			Assert.Equal(10, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}

		[Fact]
		public void TryFormatIntoTooShortSpan() {
			const string expected = "\0\0\0\0\0";

			Span<char> span = stackalloc char[5];
			var result = int.MaxValue.TryFormat(span, out var charsWritten);

			Assert.False(result);
			Assert.Equal(0, charsWritten);
			Assert.True(span.SequenceEqual(expected.AsSpan()));
		}
	}
}
