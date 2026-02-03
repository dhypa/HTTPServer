using HTTPServer.Archive.tcplistener;

namespace HTTPServer.Test;

public class Request_Test_Channel
{
    [Fact]
    public async Task Get_Request_Line()
    {
        string request = "GET / HTTP/1.1\r\nHost: localhost:42069\r\nUser-Agent: curl/7.81.0\r\nAccept: */*\r\n\r\n";
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request));
        ms.Position = 0;
        var channelReader = Reader_Channel.GetLinesChannel(ms);

        Http1Request? resultNullable = await Http1Request.FromStringAsync(channelReader);

        Assert.NotNull(resultNullable);
        Http1Request result = resultNullable!;

        Assert.NotNull(result.RequestLine);
        Assert.NotNull(result.RequestLine.HttpVersion);
        Assert.NotNull(result.RequestLine.RequestTarget);
        Assert.NotNull(result.RequestLine.HttpVersion);

        Assert.Equal("HTTP/1.1", result.RequestLine.HttpVersion);
        Assert.Equal("/", result.RequestLine.RequestTarget);
        Assert.Equal(HttpMethod.GET, result.RequestLine.Method);
    }
}
