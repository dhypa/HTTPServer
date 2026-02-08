using System.Buffers;
using System.Text;

namespace HTTPServer.Test.Reader;

public class TryReadHeaderBlockTests
{
    // If your production code already defines these, delete the two classes below
    // and update calls to point at your real implementation/namespace.
    internal static class CharsAsBytes
    {
        public static readonly byte[] Crlf = { (byte)'\r', (byte)'\n' };
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);

    [Fact]
    public void MultipleLines_WithValidLineEndings_ReadsEachLine()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n"));
        var reader = new SequenceReader<byte>(seq);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line1));
        Assert.Equal("GET / HTTP/1.1", Ascii(line1));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line2));
        Assert.Equal("Host: example.com", Ascii(line2));

        // empty line (just CRLF) ends the header block
        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line3));
        Assert.Equal("", Ascii(line3));
    }

    [Fact]
    public void MultipleLines_LastLineEndingMissing_ReturnsFalseAndDoesNotAdvance()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("A: 1\r\nB: 2"));
        var reader = new SequenceReader<byte>(seq);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line1));
        Assert.Equal("A: 1", Ascii(line1));

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line2));
        Assert.True(line2.IsEmpty);
        Assert.Equal(before, reader.Consumed); // must not advance on failure
    }

    [Fact]
    public void FirstLineEndingInvalid_LfOnly_ReturnsFullLine()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("A: 1\nB: 2\r\n"));
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(!line.IsEmpty);
        Assert.NotEqual(before, reader.Consumed);
    }

    [Fact]
    public void SingleLine_NoLineEndings_ReturnsFalseAndDoesNotAdvance()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("JustOneLine"));
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(before, reader.Consumed);
    }

    [Fact]
    public void ScatteredCrAndLfInsideLine_ButNoCrlfDelimiter_ReturnsFalse()
    {
        // Contains \r and \n but never as the sequence \r\n
        var bytes = new byte[]
        {
                (byte)'a', (byte)'\r', (byte)'b', (byte)'\n', (byte)'c', (byte)'\r', (byte)'d', (byte)'\n', (byte)'e'
        };
        var seq = SequenceHelpers.CreateSequence(bytes);
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(before, reader.Consumed);
    }

    [Fact]
    public void EmptyLine_JustCrlf_ReturnsTrueWithEmptyLineAndAdvancesPastDelimiter()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("\r\nNEXT"));
        var reader = new SequenceReader<byte>(seq);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("", Ascii(line));

        // ensure reader is now positioned at 'N'
        Assert.True(reader.TryPeek(out var b));
        Assert.Equal((byte)'N', b);
    }

    [Fact]
    public void CrlfSplitAcrossSegments_IsRecognized()
    {
        // "Hello\r" + "\nWorld\r\n"
        var seq = SequenceHelpers.CreateSequence(
            Encoding.ASCII.GetBytes("Hello\r"),
            Encoding.ASCII.GetBytes("\nWorld\r\n")
        );
        var reader = new SequenceReader<byte>(seq);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("Hello", Ascii(line));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line2));
        Assert.Equal("World", Ascii(line2));
    }

    [Fact]
    public void OnlyCarriageReturnAtEnd_NoLineEndingYet_ReturnsFalseAndDoesNotAdvance()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("ABC\r"));
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(before, reader.Consumed);
    }

    [Fact]
    public void LfOnlyLineEnding_DoesNotMatchCrlf_ReturnsFalse()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("ABC\n"));
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(before, reader.Consumed);
    }

    [Fact]
    public void BackToBackCrlf_ProducesEmptyLineBetween()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("A\r\n\r\nB\r\n"));
        var reader = new SequenceReader<byte>(seq);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l1));
        Assert.Equal("A", Ascii(l1));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l2));
        Assert.Equal("", Ascii(l2)); // empty line

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l3));
        Assert.Equal("B", Ascii(l3));
    }

    [Fact]
    public void EmptySequence_ReturnsFalse()
    {
        var seq = ReadOnlySequence<byte>.Empty;
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(before, reader.Consumed);
    }

    [Fact]
    public void ReaderStartingMidBuffer_ReadsFromCurrentPosition()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("XXA\r\nB\r\n"));
        var reader = new SequenceReader<byte>(seq);

        // move past "XX"
        reader.Advance(2);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l1));
        Assert.Equal("A", Ascii(l1));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l2));
        Assert.Equal("B", Ascii(l2));
    }

    [Fact]
    public void Success_AdvancesByLineLengthPlusDelimiter()
    {
        var seq = SequenceHelpers.CreateSequence(Encoding.ASCII.GetBytes("ABC\r\nDEF\r\n"));
        var reader = new SequenceReader<byte>(seq);

        var before = reader.Consumed;

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("ABC", Ascii(line));

        var expectedAdvance = 3 /*ABC*/ + 2 /*\r\n*/;
        Assert.Equal(before + expectedAdvance, reader.Consumed);

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line2));
        Assert.Equal("DEF", Ascii(line2));
    }
    internal static class SequenceHelpers
    {
        public static ReadOnlySequence<byte> CreateSequence(params byte[][] segments)
        {
            if (segments is null || segments.Length == 0)
                return ReadOnlySequence<byte>.Empty;

            if (segments.Length == 1)
                return new ReadOnlySequence<byte>(segments[0]);

            var first = new BufferSegment(segments[0]);
            var last = first;

            for (int i = 1; i < segments.Length; i++)
            {
                var next = new BufferSegment(segments[i]);
                last = last.Append(next);
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        public static ReadOnlySequence<byte> CreateSequence(byte[] singleSegment) =>
            singleSegment is null ? ReadOnlySequence<byte>.Empty : new ReadOnlySequence<byte>(singleSegment);

        private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
        {
            public BufferSegment(byte[] data)
            {
                Memory = data ?? Array.Empty<byte>();
            }

            public BufferSegment Append(BufferSegment next)
            {
                if (next == null) throw new ArgumentNullException(nameof(next));

                next.RunningIndex = RunningIndex + Memory.Length;
                Next = next;
                return next;
            }
        }
    }


}