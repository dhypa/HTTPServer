using System.Buffers;
using System.Text;

namespace HTTPServer.Test.Reader;

public class ReadHeadersTests
{
    // valid headers block 
    // headers but the last header is invalid
    // headers but the first header is invalid
    // valid headers block across 3 segments
    // valid headers block across 3 segments, but the segments go between a single header
    // invalid headers block in a single segment
    // request line being passed in

    // any other violations of the http RFC should be met
    // ensure headername when parsed, is normalised to lowercase, but value is left alone

    // ---------- Helpers ----------

    private static SequenceReader<byte> MakeReader(string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        var ros = new ReadOnlySequence<byte>(bytes);
        return new SequenceReader<byte>(ros);
    }

    private static SequenceReader<byte> MakeReader(params string[] segments)
    {
        // Create a segmented ReadOnlySequence<byte> where each string is one segment.
        var buffers = segments.Select(Encoding.ASCII.GetBytes).ToArray();
        var ros = CreateSequence(buffers);
        return new SequenceReader<byte>(ros);
    }

    private static ReadOnlySequence<byte> CreateSequence(params byte[][] buffers)
    {
        if (buffers.Length == 0) return ReadOnlySequence<byte>.Empty;

        BufferSegment? first = null;
        BufferSegment? last = null;

        foreach (var b in buffers)
        {
            var seg = new BufferSegment(b);
            if (first is null)
            {
                first = seg;
                last = seg;
            }
            else
            {
                last = last!.Append(seg);
            }
        }

        return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(byte[] data) => Memory = data;

        public BufferSegment Append(BufferSegment next)
        {
            next.RunningIndex = RunningIndex + Memory.Length;
            Next = next;
            return next;
        }
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            // Simple stable hash for tests
            unchecked
            {
                int h = 17;
                foreach (var b in obj) h = (h * 31) ^ b;
                return h;
            }
        }
    }

    private static Dictionary<byte[], byte[]> NewHeaders() => new(new ByteArrayComparer());

    private static byte[] Key(string s) => Encoding.ASCII.GetBytes(s);
    private static string Val(byte[] v) => Encoding.ASCII.GetString(v);

    // ---------- Tests ----------

    [Fact]
    public void Valid_headers_block_single_segment()
    {
        // includes: normal header, leading spaces in value should be trimmed, empty line terminates block
        var reader = MakeReader(
            "Host: example.com\r\n" +
            "Content-Type:  text/plain\r\n");

        var headers = NewHeaders();
        Http1Parser.ReadHeaders(ref reader, headers);

        Assert.Equal("example.com", Val(headers[Key("host")]));                 // normalized to lowercase
        Assert.Equal("text/plain", Val(headers[Key("content-type")]));          // TrimStart on value
    }

    [Fact]
    public void Headers_but_last_header_invalid_throws()
    {
        var reader = MakeReader(
            "Host: example.com\r\n" +
            "BadHeaderWithoutColon\r\n");

        var headers = NewHeaders();

        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }

    [Fact]
    public void Headers_but_first_header_invalid_throws()
    {
        var reader = MakeReader(
            "NoColonHere\r\n" +
            "Host: example.com\r\n");

        var headers = NewHeaders();
        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }

    [Fact]
    public void Valid_headers_block_across_3_segments()
    {
        // split across segments at line boundaries
        var reader = MakeReader(
            "Host: example.com\r\n",
            "X-Test: 123\r\n");

        var headers = NewHeaders();
        Http1Parser.ReadHeaders(ref reader, headers);

        Assert.Equal("example.com", Val(headers[Key("host")]));
        Assert.Equal("123", Val(headers[Key("x-test")]));
    }

    [Fact]
    public void Valid_headers_block_across_3_segments_split_inside_single_header()
    {
        // segments split mid-header-name, mid-value, and mid-CRLF sequences
        var reader = MakeReader(
            "Ho",
            "st: examp",
            "le.com\r\n\r\n");

        var headers = NewHeaders();
        Http1Parser.ReadHeaders(ref reader, headers);

        Assert.Equal("example.com", Val(headers[Key("host")]));
    }

    [Fact]
    public void Invalid_headers_block_single_segment_invalid_name_char_throws()
    {
        // space in header name is invalid (token)
        var reader = MakeReader(
            "Bad Name: value\r\n");

        var headers = NewHeaders();
        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }

    [Fact]
    public void Request_line_being_passed_in_throws()
    {
        // If someone accidentally calls ReadHeaders with a request line still in the reader
        var reader = MakeReader(
            "GET / HTTP/1.1\r\n" +
            "Host: example.com\r\n");

        var headers = NewHeaders();
        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }

    [Fact]
    public void Duplicate_header_names_merge_with_comma_per_rfc7230()
    {
        var reader = MakeReader(
            "Accept: text/plain\r\n" +
            "Accept: application/json\r\n");

        var headers = NewHeaders();
        Http1Parser.ReadHeaders(ref reader, headers);

        Assert.Equal("text/plain,application/json", Val(headers[Key("accept")]));
    }

    [Fact]
    public void Header_value_left_alone_except_leading_ows_trimmed()
    {
        // Leading OWS trimmed; internal and trailing spaces preserved (your code uses TrimStart only)
        var reader = MakeReader(
            "X:   a  b  \r\n");

        var headers = NewHeaders();
        Http1Parser.ReadHeaders(ref reader, headers);

        Assert.Equal("a  b  ", Val(headers[Key("x")])); // trailing spaces remain
    }

    [Fact]
    public void Empty_header_name_throws()
    {
        var reader = MakeReader(
            ": value\r\n");

        var headers = NewHeaders();
        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }

    [Fact]
    public void Header_name_with_control_char_throws()
    {
        // 0x7F DEL is not a valid token char; adjust if your IsTokenChar differs
        var bytes = Encoding.ASCII.GetBytes("X-\x7F: value\r\n");
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        var headers = NewHeaders();
        try
        {
            Http1Parser.ReadHeaders(ref reader, headers);
            Assert.Fail();
        }
        catch (FormatException)
        {

        }
    }
}
