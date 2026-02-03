using System;
using System.Collections.Generic;
using System.Text;

namespace HTTPServer.Test;

internal static class UtilityExtentions
{
    public static string GetString(this ReadOnlySpan<byte> span)
    {
        return Encoding.ASCII.GetString(span);
    }
}
