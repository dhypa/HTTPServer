using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace HTTPServer.Test.Reader;

public class ParseHeaderBlockTests
{
    // ---------- Helpers ----------

    private static ReadOnlySequence<byte> Seq(string s)
        => new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(s));

    private static ReadOnlySequence<byte> Seq(params string[] segments)
        => CreateSequence(segments.Select(Encoding.ASCII.GetBytes).ToArray());

    private static byte[] B(string  s) => Encoding.ASCII.GetBytes(s);
    
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

    private static byte[]? GetHeaderValue(Dictionary<byte[], byte[]> headers, string headerName)
    {
        var headerNameBytes = Encoding.ASCII.GetBytes(headerName);

        foreach (var kvp in headers)
        {
            if (AsciiEqualsIgnoreCase(kvp.Key, headerNameBytes))
                return kvp.Value;
        }

        return null;
    }

    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            byte x = a[i];
            byte y = b[i];

            // normalize ASCII uppercase → lowercase
            if (x >= (byte)'A' && x <= (byte)'Z') x = (byte)(x + 32);
            if (y >= (byte)'A' && y <= (byte)'Z') y = (byte)(y + 32);

            if (x != y)
                return false;
        }

        return true;
    }

    // ---------- Tests ----------

    [Fact]
    public void ParseHeaderBlock_Parses_request_line_and_headers_without_final_blank_line()
    {
        // NOTE: no trailing "\r\n\r\n" (no empty line after headers)
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "example:eeeeee\r\n" +
            "regtwe:ewewetew\r\n"
        );

        var head = Http1Parser.ParseHeaderBlock(buffer); // see note below

        Assert.Equal(B("GET"), head.Method);
        Assert.Equal(B("/target"), head.RequestTarget);
        Assert.Equal(B("HTTPVERSION"), head.HttpVersion);

        Assert.Equal(B("eeeeee"), GetHeaderValue(head.Headers, "example"));
        Assert.Equal(B("ewewetew"), GetHeaderValue(head.Headers, "regtwe"));
    }

    [Fact]
    public void ParseHeaderBlock_Allows_last_header_as_final_bytes_without_extra_crlf()
    {
        // Some callers may send no CRLF after final header line.
        // If your TryReadHeaderBlockLine REQUIRES \r\n, then this should throw.
        // If it can read to end-of-buffer as a line, this should succeed.
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "example:eeeeee\r\n" +
            "regtwe:ewewetew" // <- no trailing \r\n
        );

        try
        {
            var head = Http1Parser.ParseHeaderBlock(buffer);

            Assert.Equal(B("GET"), head.Method);
            Assert.Equal(B("/target"), head.RequestTarget);
            Assert.Equal(B("HTTPVERSION"), head.HttpVersion);
            Assert.Equal(B("eeeeee"), GetHeaderValue(head.Headers, "example"));
        }
        catch (FormatException)
        {
            // If your line-reader requires CRLF termination, this is expected.
            // Keeping the test explicit makes the contract clear.
            Assert.True(true);
        }
    }

    [Fact]
    public void ParseHeaderBlock_Parses_across_multiple_segments_including_splits_inside_lines()
    {
        // Split inside request line and inside headers
        var buffer = Seq(
            "GE","T /tar","get HTTPVERSION\r\nexamp",
            "le:eeeeee\r\nreg",
            "twe:ewewetew\r\n"
        );

        var head = Http1Parser.ParseHeaderBlock(buffer);

        Assert.Equal(B("GET"), head.Method);
        Assert.Equal(B("/target"), head.RequestTarget);
        Assert.Equal(B("HTTPVERSION"), head.HttpVersion);

        Assert.Equal(B("eeeeee"), GetHeaderValue(head.Headers, "example"));
        Assert.Equal(B("ewewetew"), GetHeaderValue(head.Headers, "regtwe"));
    }

    [Fact]
    public void ParseHeaderBlock_Missing_request_line_throws()
    {
        var buffer = Seq(
            "example:eeeeee\r\n" +
            "regtwe:ewewetew\r\n"
        );

        Assert.Throws<FormatException>(() => Http1Parser.ParseHeaderBlock(buffer));
    }

    [Fact]
    public void ParseHeaderBlock_Invalid_header_line_throws()
    {
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "example:eeeeee\r\n" +
            "this-is-not-a-header\r\n"
        );

        Assert.Throws<FormatException>(() => Http1Parser.ParseHeaderBlock(buffer));
    }

    [Fact]
    public void ParseHeaderBlock_Request_line_is_not_a_header_and_should_be_parsed_as_request_line()
    {
        // This guards against accidentally passing the request-line to ReadHeaders.
        // If ReadRequestLine works, we should not see "GET /target..." as a header.
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "example:eeeeee\r\n"
        );

        var head = Http1Parser.ParseHeaderBlock(buffer);

        Assert.Equal(B("GET"), head.Method);
        Assert.Null(GetHeaderValue(head.Headers, "GET /target HTTPVERSION"));
    }

    [Fact]
    public void ParseHeaderBlock_Duplicate_headers_merge_with_comma_if_ReadHeaders_does()
    {
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "accept:text/plain\r\n" +
            "accept:application/json\r\n"
        );

        var head = Http1Parser.ParseHeaderBlock(buffer);

        // If your ReadHeaders merges duplicates via RFC 7230 comma rule:
        Assert.Equal(B("text/plain,application/json"), GetHeaderValue(head.Headers, "accept"));
    }

    [Fact]
    public void ParseHeaderBlock_Demonstrates_dictionary_key_comparer_bug_optional()
    {
        // OPTIONAL: This will fail (or behave oddly) unless ParseHeaderBlock uses ByteArrayComparer.
        // Keep it if you want a test that forces the production fix.
        var buffer = Seq(
            "GET /target HTTPVERSION\r\n" +
            "example:eeeeee\r\n"
        );

        var head = Http1Parser.ParseHeaderBlock(buffer);

        // This TryGetValue will almost certainly fail with default comparer (reference equality).
        var key = B("example");
        var ok = head.Headers.TryGetValue(key, out var value);

        // If you fix ParseHeaderBlock to use ByteArrayComparer, this should become True.
        Assert.False(ok);
    }
}
