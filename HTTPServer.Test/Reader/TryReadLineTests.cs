using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using HTTPServer.Test;

namespace HTTPServer.Test.Reader;

public class TryReadLineTests
{
    static SequenceReader<byte> MakeReader(byte[] bytes)
    {
        var ros = new ReadOnlySequence<byte>(bytes);
        return new SequenceReader<byte>(ros);
    }
    static byte[] B(string s) => Encoding.ASCII.GetBytes(s);

    [Fact]
    public void TryReadLine_ReadsUpToCrlf_AndAdvancesPastDelimiter()
    {
        var reader = MakeReader(B("HELLO\r\nWORLD"));
        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("HELLO", Encoding.ASCII.GetString(line));

        // Remaining should start with WORLD
        reader.TryReadTo(out ReadOnlySpan<byte> rest, (byte)0, advancePastDelimiter: false);
        // The above isn't a great "rest" extractor; alternative below.
    }
    [Fact]
    public void TryReadLine_ReadsUpToCrlf_AndReaderNowAtNextByte()
    {
        var reader = MakeReader(B("HELLO\r\nWORLD"));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("HELLO", Encoding.ASCII.GetString(line));

        // Read next 5 bytes directly
        Assert.True(reader.TryRead(out byte w1) && w1 == (byte)'W');
        Assert.True(reader.TryRead(out byte w2) && w2 == (byte)'O');
        Assert.True(reader.TryRead(out byte w3) && w3 == (byte)'R');
        Assert.True(reader.TryRead(out byte w4) && w4 == (byte)'L');
        Assert.True(reader.TryRead(out byte w5) && w5 == (byte)'D');
    }
    [Fact]
    public void TryReadLine_EmptyLine_ReturnsEmptySpan()
    {
        var reader = MakeReader(B("\r\nABC"));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal(0, line.Length);

        Assert.True(reader.TryRead(out var a));
        Assert.Equal((byte)'A', a);
    }
    [Fact]
    public void TryReadLine_CanReadMultipleLines()
    {
        var reader = MakeReader(B("A\r\nB\r\nC\r\n"));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l1));
        Assert.Equal("A", Encoding.ASCII.GetString(l1));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l2));
        Assert.Equal("B", Encoding.ASCII.GetString(l2));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var l3));
        Assert.Equal("C", Encoding.ASCII.GetString(l3));

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out _));
    }

    [Fact]
    public void TryReadLine_NoCrlf_ReturnsFalse_AndDoesNotAdvance()
    {
        var bytes = B("NO_DELIMITER");
        var reader = MakeReader(bytes);

        var startConsumed = reader.Consumed;

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal(0, line.Length);               // out span is default
        Assert.Equal(startConsumed, reader.Consumed); // no advance
    }
    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    static SequenceReader<byte> MakeSegmentedReader(params byte[][] parts)
    {
        var first = new BufferSegment(parts[0]);
        var last = first;
        for (int i = 1; i < parts.Length; i++)
            last = last.Append(parts[i]);

        var seq = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        return new SequenceReader<byte>(seq);
    }

    [Fact]
    public void TryReadLine_CrlfAcrossSegments_Works()
    {
        var reader = MakeSegmentedReader(
            B("HELLO\r"),
            B("\nWORLD")
        );

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("HELLO", Encoding.ASCII.GetString(line));

        Assert.True(reader.TryRead(out var w));
        Assert.Equal((byte)'W', w);
    }
    [Fact]
    public void TryReadLine_IgnoresStandaloneCarriageReturn()
    {
        var reader = MakeReader(B("A\rB\r\nC"));

        Assert.True(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line));
        Assert.Equal("A\rB", Encoding.ASCII.GetString(line));
    }

    [Fact]
    public void TryReadLine_EofWithoutCrlf_ReturnsNothing_Spec()
    {
        var reader = MakeReader(B("LASTLINE"));

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line)); // currently FALSE
    }
    [Fact]
    public void TryReadLine_EmptySequence_ReturnsFalse_Spec()
    {
        var reader = MakeReader(Array.Empty<byte>());

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out _));
    }
    [Fact]
    public void TryReadLine_EofAfterCarriageReturn_ReturnsRemainingBytes_Spec()
    {
        var reader = MakeReader(B("A\r"));

        Assert.False(Http1Parser.TryReadHeaderBlockLine(ref reader, out var line)); // currently FALSE
    }
}
