using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace HTTPServer;

internal class Program
{
    static UTF8Encoding encoder = new();
    static async Task Main(string[] args)
    {
        var path = Path.Combine(Environment.CurrentDirectory, "message.txt");
        using (FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var reader = Reader.GetLinesChannel(fs);

            await foreach (var line in reader.ReadAllAsync())
            {
                Console.Write("read: {0}\n", encoder.GetString(line));
            }
        }
    }
}