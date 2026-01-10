using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace HTTPServer;

internal class Program
{
    static UTF8Encoding encoder = new();
    static void Main(string[] args)
    {
        var path = Path.Combine(Environment.CurrentDirectory, "message.txt");

        Span<byte> buffer = stackalloc byte[8];
        using (FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            int bytesRead;
            ReadOnlySpan<byte> currStr = null;

            while ((bytesRead = fs.Read(buffer)) > 0)
            {
                PrintChunk(buffer, currStr);
            }
        }
    }

    static void PrintChunk(ReadOnlySpan<byte> value, ReadOnlySpan<byte> currStr)
    {
        ReadOnlySpan<byte> last = default;

        var parts = value.Split((byte)('\n'));

        foreach(var part in parts)
        {
            Console.Write("read: {0}{1}\n", encoder.GetString(currStr), encoder.GetString(value[part.Start .. part.End]));
            currStr = null;
            last = value[part.Start..part.End];
        }
        currStr = last;
    }

    //public static ReadOnlySpan<T> Concat<T>(this ReadOnlySpan<T> span0, ReadOnlySpan<T> span1)  
    //{
    //    var dest = new T[span0.Length + span1.Length].AsSpan();

    //    span0.CopyTo(dest);
    //    span1.CopyTo(dest.Slice(span0.Length));
    //    return dest;
    //}
}