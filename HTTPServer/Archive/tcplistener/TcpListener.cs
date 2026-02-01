using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace HTTPServer.Archive.tcplistener;

public class TcpListener
{
    static UTF8Encoding encoder = new();
    public static async Task Run(string[] args)
    {

        var ip = new IPEndPoint(IPAddress.Any, 42069);

        System.Net.Sockets.TcpListener listener = new(ip);
        listener.Start();

        using TcpClient handler = await listener.AcceptTcpClientAsync();
        await using NetworkStream stream = handler.GetStream();

        var reader = Reader.GetLinesChannel(stream);

        await foreach (var line in reader.ReadAllAsync())
        {
            Console.Write("read: {0}\n", encoder.GetString(line));
        }
    }
}