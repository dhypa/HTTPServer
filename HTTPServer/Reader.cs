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
            if(!reader.TryReadTo(out ReadOnlySpan<byte> _, CharsAsBytes.r, false))
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
    private static RequestLine ParseHeaderBlock(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        if(reader.TryReadTo(out ReadOnlySpan<byte> methodSpan, CharsAsBytes.Space, false))
        {


        }
    }

    private static bool TryReadLine(ref SequenceReader<byte> reader, out ReadOnlySpan<byte> line)
    {
        if(reader.TryReadTo(out line, CharsAsBytes.Crlf, false))
        {
            reader.Advance(2);
        }

    }
}
