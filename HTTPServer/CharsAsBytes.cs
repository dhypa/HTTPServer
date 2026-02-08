using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace HTTPServer;

public static class CharsAsBytes
{
    public const byte Cr = (byte)'\r';
    public const byte Nl = (byte)'\n';
    public const byte Space = (byte)' ';
    public const byte Colon = (byte)':';

    public static readonly byte[] CrlfCrlf = [
        (byte)'\r',
        (byte)'\n',
        (byte)'\r',
        (byte)'\n'
    ];

    public static readonly byte[] Crlf = [
        (byte)'\r',
        (byte)'\n',
    ];

    public static bool TryMatchCrlf(ReadOnlySequence<byte> buffer, SequencePosition position)
    {
        var slice = buffer.Slice(position);
        if (slice.Length < 2)
        {
            return false;
        }

        var term = Crlf;

        var first = slice.FirstSpan;

        // fast path
        if (first.Length >= 2)
        {
            return
                first[0] == term[0] &&
                first[1] == term[1];
        }

        // slow path
        // crosses segment boundary
        var reader = new SequenceReader<byte>(buffer);
        return
            reader.TryRead(out var b0) && b0 == term[0] &&
            reader.TryRead(out var b1) && b1 == term[1];


    }
    public static bool TryMatchCrlfCrlf(ReadOnlySequence<byte> buffer, SequencePosition position)
    {
        var slice = buffer.Slice(position);

        if (slice.Length < 4)
        {
            return false;
        }

        var term = CrlfCrlf;

        var first = slice.FirstSpan;

        // fast path
        if (first.Length >= 4)
        {
            return
                first[0] == term[0] &&
                first[1] == term[1] &&
                first[2] == term[2] &&
                first[3] == term[3];
        }

        // slow path
        // crosses segment boundary
        var reader = new SequenceReader<byte>(buffer);

        return
            reader.TryRead(out var b0) && b0 == term[0] &&
            reader.TryRead(out var b1) && b1 == term[1] &&
            reader.TryRead(out var b2) && b2 == term[2] &&
            reader.TryRead(out var b3) && b3 == term[3];
    }

    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Length != y.Length) return false;

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) return false;
            }
            return true;
        }

        public int GetHashCode([DisallowNull] byte[] obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in obj)
                {
                    hash = hash * 31 + c.GetHashCode();
                }
                return hash;
            }
        }
    }
}
