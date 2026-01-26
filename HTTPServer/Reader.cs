using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace HTTPServer;

public static class Reader
{
    static UTF8Encoding encoder = new();
    public static ChannelReader<byte[]> GetLinesChannel(Stream s)
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        var writer = channel.Writer;
        var t = Task.Run(async () =>
        {
            Exception? ex = null;
            try
            {
                int bytesRead;
                byte[] buffer = new byte[8];
                ArrayBufferWriter<byte> currStr = new ArrayBufferWriter<byte>(16);

                while ((bytesRead = await s.ReadAsync(buffer)) > 0)
                {

                    var parts = buffer.AsSpan(0, bytesRead).Split((byte)'\n');
                    bool seen = false;

                    foreach (var part in parts)
                    {
                        if (seen is false)
                        {
                            currStr.Write(buffer[part.Start..part.End]);
                            seen = true;
                            continue;
                        }

                        writer.TryWrite(currStr.WrittenMemory.ToArray());
                        currStr.Clear();
                        currStr.Write(buffer[part.Start..part.End]);
                    }
                }
                if (currStr.WrittenCount > 0)
                {
                    await writer.WriteAsync(currStr.WrittenMemory.ToArray());
                }
            }
            catch (Exception excep)
            {
                ex = excep;
            }
            finally
            {
                writer.TryComplete(ex);
            }
        });
        return channel.Reader;
    }
}
