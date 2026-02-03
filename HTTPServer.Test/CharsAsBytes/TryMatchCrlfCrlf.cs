namespace HTTPServer.Test.CharsAsBytes;

using System.Buffers;
using Xunit;
using HTTPServer;

public class TryMatchCrlfCrlfTests
{
    [Fact]
    public void Match_SingleSegment_Success()
    {
        // Arrange
        byte[] data = new byte[]
        {
            (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'
        };

        var seq = new ReadOnlySequence<byte>(data);

        // Act
        bool result = CharsAsBytes.TryMatchCrlfCrlf(seq, seq.Start);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Match_MultiSegment_Success()
    {
        // Arrange
        var first = new byte[] { (byte)'\r', (byte)'\n' };
        var second = new byte[] { (byte)'\r', (byte)'\n' };

        var segment2 = new BufferSegment(second);
        var segment1 = new BufferSegment(first, segment2);

        var seq = new ReadOnlySequence<byte>(segment1, 0, segment2, second.Length);

        // Act
        bool result = CharsAsBytes.TryMatchCrlfCrlf(seq, seq.Start);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NoMatch_WrongBytes()
    {
        // Arrange
        byte[] data = new byte[]
        {
            (byte)'A', (byte)'B', (byte)'C', (byte)'D'
        };

        var seq = new ReadOnlySequence<byte>(data);

        // Act
        bool result = CharsAsBytes.TryMatchCrlfCrlf(seq, seq.Start);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NoMatch_TooShort()
    {
        // Arrange
        byte[] data = new byte[]
        {
            (byte)'\r', (byte)'\n', (byte)'\r' // only 3 bytes
        };

        var seq = new ReadOnlySequence<byte>(data);

        // Act
        bool result = CharsAsBytes.TryMatchCrlfCrlf(seq, seq.Start);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NoMatch_SequenceDoesNotStartAtPosition()
    {
        // Arrange
        byte[] data = new byte[]
        {
            (byte)'X',
            (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'
        };

        var seq = new ReadOnlySequence<byte>(data);

        // Act — start at index 1 instead of 0
        var pos = seq.GetPosition(1);
        bool result = CharsAsBytes.TryMatchCrlfCrlf(seq, pos);

        // Assert
        Assert.True(result); // should match here

        // Act — start at index 0 (should NOT match)
        bool result2 = CharsAsBytes.TryMatchCrlfCrlf(seq, seq.Start);

        // Assert
        Assert.False(result2);
    }

    //
    // Helper: minimal BufferSegment implementation for multi-segment tests
    //
    private class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(byte[] memory)
        {
            Memory = memory;
        }

        public BufferSegment(byte[] memory, BufferSegment next)
        {
            Memory = memory;
            Next = next;
            next.RunningIndex = memory.Length;
        }
    }

    //
    // Include the method under test so the file is self-contained
    //
    public static readonly byte[] CrlfCrlf = new byte[]
    {
        (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'
    };
}

