using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace HTTPServer;

public interface IHttp1Parser
{
    public static abstract Task<Http1Request> ReadRequestAsync(PipeReader reader, CancellationToken ct = default);
}

public class Http1Parser: IHttp1Parser
{
    private const int MaxHeaderBytes = 64 * 1024; // 64KB
    public static async Task<Http1Request> ReadRequestAsync(PipeReader reader, CancellationToken ct = default)
    {
        // Read whole header block up to \r\n\r\n
        var (headerSequence, bodySequence) = await ReadHeaderBlockAsync(reader, ct);
        var requestLine = ParseHeaderBlock(bodySequence);

        // get body

        throw new NotImplementedException();
    }

    // Take in reader
    // return pointer to header buffer/span/whatever the fuck
    private static async Task<(ReadOnlySequence<byte> HeaderBlock, ReadOnlySequence<byte> BodyRemainder)>
        ReadHeaderBlockAsync(PipeReader reader, CancellationToken ct)
    {
        long totalBuffered = 0;

        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            var buffer = result.Buffer;

            totalBuffered += buffer.Length;
            if (totalBuffered > MaxHeaderBytes)
            {
                throw new FormatException($"HTTP Header length exceeds limit {MaxHeaderBytes} bytes");
            }

            if (TryFindCrlfCrlf(buffer, out var headerEndPosition))
            {
                var headerBlock = buffer.Slice(0, headerEndPosition);

                // skip over CrlfCrlf
                var afterHeaders = buffer.GetPosition(4, headerEndPosition);
                var bodyBlock = buffer.Slice(afterHeaders, buffer.End);

                reader.AdvanceTo(afterHeaders, buffer.End);

                return (headerBlock, bodyBlock);
            }


            // if stream ended, throw exception
            if (result.IsCompleted)
            {
                throw new EndOfStreamException("Stream completed before end of headers block found");
            }

            // advance reader but do nothing so we keep reading
            reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }
    private static bool TryFindCrlfCrlf(ReadOnlySequence<byte> buffer, out SequencePosition headerEndPosition)
    {
        var reader = new SequenceReader<byte>(buffer);

        while (!reader.End)
        {
            // read to next \r
            // if we can't find one, break out so more data can be read
            if (!reader.TryReadTo(out ReadOnlySpan<byte> _, CharsAsBytes.Cr, false))
            {
                break;
            }
            var candidate = reader.Position;
            if (CharsAsBytes.TryMatchCrlfCrlf(buffer, candidate))
            {
                headerEndPosition = candidate;
                return true;
            }

            // advance past found \r
            reader.Advance(1);
        }
        headerEndPosition = default;
        return false;
    }

    // Parse header block buffer/span/whatever the fuck
    internal static Head ParseHeaderBlock(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        // parse request line
        if (!TryReadHeaderBlockLine(ref reader, out var requestLineSpan))
        {
            throw new FormatException("Invalid HTTP request: Missing request line");
        }
        ReadRequestLine(requestLineSpan, out var methodSpan, out var targetSpan, out var httpVersionSpan);

        // parse headers

        var headers = new Dictionary<byte[], byte[]>(12, new CharsAsBytes.ByteArrayComparer());

        ReadHeaders(ref reader, headers);

        return new Head()
        {
            Headers = headers,
            HttpVersion = httpVersionSpan.ToArray(),
            Method = methodSpan.ToArray(),
            RequestTarget = targetSpan.ToArray()
        };
    }

    private static readonly byte[] HeaderValueTrimCandidates = [(byte)' ', (byte)'\t'];
    internal static void ReadHeaders(ref SequenceReader<byte> reader, Dictionary<byte[], byte[]> headers)
    {
        while (TryReadHeaderBlockLine(ref reader, out ReadOnlySpan<byte> line))
        {
            if (line.Length == 0)
                break;

            int sep = line.IndexOf((byte)':');
            if (sep < 1)
                throw new FormatException("Invalid header: missing or misplaced colon");

            var nameBytes = line[..sep];
            var valueBytes = line[(sep + 1)..];

            // Validate header name
            foreach (byte b in nameBytes)
            {
                if (!IsTokenChar(b))
                    throw new FormatException("Invalid header name");
            }

            var name = nameBytes.Trim(HeaderValueTrimCandidates).ToArray();
            for(int i = 0; i < name.Length; i++) {
                var b = name[i];
                if('A' <= b && b <= 'Z')
                {
                    name[i] += 32;
                }
            }

            var value = valueBytes.TrimStart(HeaderValueTrimCandidates).ToArray();

            if (headers.TryGetValue(name, out var existing))
            {
                headers[name] = [..existing, (byte)',', ..value]; // RFC 7230 merge rule
            }
            else
            {
                headers[name] = value;
            }
        }
    }   

    private static bool IsTokenChar(byte b)
    {
        // RFC 7230 token definition
        return b > 32 && b < 127 && "()<>@,;:\\\"/[]?={} ".IndexOf((char)b) == -1;
    }


    internal static bool TryReadHeaderBlockLine(ref SequenceReader<byte> reader, out ReadOnlySpan<byte> line)
    {
        if (reader.TryReadTo(out line, CharsAsBytes.Crlf, false))
        {
            reader.Advance(2);
            return true;
        }
        return false;
    }

    // GET /chungus HTTP1.1

    internal static void ReadRequestLine(ReadOnlySpan<byte> requestLine, out ReadOnlySpan<byte> method, out ReadOnlySpan<byte> target, out ReadOnlySpan<byte> httpVersion)
    {
        int methodPosition = requestLine.IndexOf(CharsAsBytes.Space);
        if (methodPosition == -1)
            throw new FormatException("Invalid HTTP request line: Malformed method");
        method = requestLine.Slice(0, methodPosition);

        int targetPosition = requestLine.Slice(methodPosition + 1).IndexOf(CharsAsBytes.Space);
        if (targetPosition == -1)
            throw new FormatException("Invalid HTTP request line: Malformed target");

        target = requestLine.Slice(methodPosition + 1, targetPosition);
        httpVersion = requestLine.Slice(methodPosition + targetPosition + 2);

        if (method.Length == 0 || target.Length == 0 || httpVersion.Length == 0)
        {
            throw new FormatException("Malformed request line");
        }
    }
}