using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace HTTPServer;

public class Http1Parser
{
    private const int MaxHeaderBytes = 64 * 1024; // 64KB
    public static async Task<Http1Request> ReadRequestAsync(PipeReader reader, CancellationToken ct = default)
    {
        // Read whole header block up to \r\n\r\n
        var (headerSequence, bodySequence) = await ReadHeaderBlockAsync(reader, ct);
        var requestLine = ParseHeaderBlock(bodySequence);

        // get body

    }

    // Take in reader
    // return pointer to header buffer/span/whatever the fuck
    private static async Task<(ReadOnlySequence<byte> HeaderBlock, ReadOnlySequence<byte>BodyRemainder)> 
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

            if(TryFindCrlfCrlf(buffer, out var headerEndPosition))
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
            // don't advance - compare all 4 bytes
            if(!reader.TryReadTo(out ReadOnlySpan<byte> _, CharsAsBytes.Cr, false))
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
    private static Head ParseHeaderBlock(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        // parse request line
        if(!TryReadLine(ref reader, out var requestLineSpan))
        {
            throw new FormatException("Invalid HTTP request: Missing request line");
        }
        ReadRequestLine(requestLineSpan, out var methodSpan, out var targetSpan, out var httpVersionSpan);

        // parse headers

        var headers = new Dictionary<string, string>(12,StringComparer.OrdinalIgnoreCase);

        while (TryReadLine(ref reader, out ReadOnlySpan<byte> line))
        {
            if(line.Length == 0)
            {
                // we will assume 
                break;
            }
            var seperator = line.IndexOf(CharsAsBytes.Colon);
            if(seperator is -1)
            {
                throw new FormatException("Invalid HTTP header line: Missing colon separator");
            }

            var nameSpan = line.Slice(0, seperator).Trim(CharsAsBytes.Space);
            var valueSpan = line.Slice(seperator + 1).Trim(CharsAsBytes.Space);

            // TryAdd ensures only the first key is kept
            // if my producers are assholes and send duplicate headers 
            // this is the correct behavior per RFC 7230 Section 3.2.2
            headers.TryAdd(
                Encoding.ASCII.GetString(nameSpan), 
                Encoding.ASCII.GetString(valueSpan)
            );
        }


    }

    private static bool TryReadLine(ref SequenceReader<byte> reader, out ReadOnlySpan<byte> line)
    {
        if(reader.TryReadTo(out line, CharsAsBytes.Crlf, false))
        {
            reader.Advance(2);
            return true;
        }
        return false;
    }

    private static void ReadRequestLine(ReadOnlySpan<byte> requestLine, out ReadOnlySpan<byte> method, out ReadOnlySpan<byte> target, out ReadOnlySpan<byte> httpVersion)
    {
        int methodPosition = requestLine.IndexOf(CharsAsBytes.Space);
        if(methodPosition == -1)
            throw new FormatException("Invalid HTTP request line: Malformed method");
        
        int targetPosition = requestLine.Slice(methodPosition + 1).IndexOf(CharsAsBytes.Space);
        if (targetPosition == -1)
            throw new FormatException("Invalid HTTP request line: Malformed target");

        method = requestLine.Slice(0, methodPosition);
        target = requestLine.Slice(methodPosition + 1, targetPosition);
        httpVersion = requestLine.Slice(targetPosition + 1);  
    }
}
