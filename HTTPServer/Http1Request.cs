using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace HTTPServer;

public class Http1Request
{
    public Head Head { get; set; }
    public byte[]? Body;
    public Dictionary<char[], char[]> Headers => Head.Headers;

    public static async Task<Http1Request> FromStringAsync(ChannelReader<byte[]> reader, CancellationToken ct = default)
    {
        var requestLine = await ReadLineAsync(reader, ct);
        if (requestLine == null)
        {
            throw new FormatException("Request line is null");
        }
        var requestLineString = Encoding.ASCII.GetString(requestLine);

        var parts = requestLineString.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Bad request line: '{requestLineString}' ");
        }

        if (!HttpMethodUtils.TryParseString(parts[0], out var parsedHttpMethod))
        {
            throw new FormatException($"Bad request line: '{requestLineString}' ");
        }

        var request = new Http1Request()
        {
            Head = new Head()
            {
                Method = parsedHttpMethod.ToString(),
                HttpVersion = parts[2],
                RequestTarget = parts[1]
            }
        };

        Dictionary<string, string> headers = new();
        while (true)
        {
            var lineBytes = await ReadLineAsync(reader, ct);
            if (lineBytes is null or { Length: 0 })
            {
                break;
            }

            var line = Encoding.ASCII.GetString(lineBytes);

            var colon = line.IndexOf(':');
            if (colon is -1)
            {
                throw new FormatException($"Bad header line: '{line}'");
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            headers[name] = value;
        }

        ArrayBufferWriter<byte> abw = new(8);

        while (true)
        {
            var bytes = await ReadLineAsync(reader, ct);
            if (bytes is null)
            {
                break;
            }
            abw.Write(bytes);
        }

        request.Body = abw.WrittenMemory.ToArray();

        return request;
    }
    private static async ValueTask<byte[]?> ReadLineAsync(ChannelReader<byte[]> lines, CancellationToken ct)
    {
        if(lines.TryRead(out var line)){
            return line;
        }

        while (await lines.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            if (lines.TryRead(out line))
                return line;
        }

        return null;
    }
    // POST /coffee HTTP/1.1
    // Host: localhost:42069
    // User-Agent: curl/7.81.0
    // Accept: */*
    // Content-Length: 21

    // {"flavor":"dark mode"}
}
public class Head
{
    public string Method { get; set; }
    public string RequestTarget { get; set; }
    public string HttpVersion { get; set; }
    public Dictionary<char[], char[]> Headers { get; set; } = new();
}

public enum HttpMethod
{
    GET = 0,
    POST = 1,
    PUT = 2,
    DELETE = 3
}
public static class HttpMethodUtils
{
    public static bool TryParseString(string str, out HttpMethod? httpMethod)
    {
        if (Enum.TryParse(typeof(HttpMethod), str, out var parsedMethod))
        {
            httpMethod = (HttpMethod)parsedMethod;

            return true;
        }
        httpMethod = null;
        return false;
    }

    public static bool TryParseHttpMethodSpan(ref ReadOnlySpan<byte> candidate)
    {

        throw new NotImplementedException();
    }
}