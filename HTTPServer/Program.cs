using System;
using System.Collections.Generic;
using System.Text;

namespace HTTPServer;

internal class Program
{
    static async Task Main(string[] args)
    {
        await Archive.tcplistener.TcpListener.Run([]);
    }
}
