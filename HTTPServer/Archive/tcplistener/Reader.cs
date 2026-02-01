using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace HTTPServer.Archive.tcplistener;

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
                ArrayBufferWriter<byte> curr = new ArrayBufferWriter<byte>(16);
                Span<byte> currSpan = null;

                bool lastWasCR = false;

                while ((bytesRead = await s.ReadAsync(buffer)) > 0)
                {

                    for (int i = 0; i < bytesRead; i++)
                    {

                        if (lastWasCR)
                        {
                            if (buffer[i] == (byte)'\r')
                            {
                                lastWasCR = true;
                                continue;
                            }

                            if (buffer[i] == (byte)'\n')
                            {
                                writer.TryWrite(curr.WrittenMemory.ToArray());
                                curr.Clear();
                                lastWasCR = false;
                                continue;
                            }
                            currSpan = curr.GetSpan(1);
                            currSpan[0] = (byte)'\r';
                            curr.Advance(1);
                            // We don't continue as current byte has not been parsed
                        }

                        if (buffer[i] == (byte)'\r')
                        {
                            lastWasCR = true;
                            continue;
                        }

                        currSpan = curr.GetSpan(1);
                        currSpan[0] = buffer[i];
                        curr.Advance(1);
                    }
                }

                if (curr.WrittenCount > 0)
                {
                    await writer.WriteAsync(curr.WrittenMemory.ToArray());
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }
            finally
            {
                writer.TryComplete(ex);
            }
        });
        return channel.Reader;
    }
}
