using System;

namespace TK2.Customization;

// Pure byte operations: preserve every untouched channel when cloning a GPU-only mesh.
internal static class MirrorGeometry
{
    internal static (float X, float Y, float Z, float W) Rotation(float x, float y, float z, float w) => (x, -y, -z, w);

    internal static void NegateChannel(byte[] bytes, int count, int stride, int offset, int format)
    {
        int size = format == 0 ? 4 : format == 1 || format == 5 ? 2 : format == 3 ? 1 : 0;
        if (size == 0) throw new NotSupportedException("Mirror requires Float32, Float16, SNorm8 or SNorm16 geometry channels.");
        if (count < 0 || stride < size || offset < 0 || offset + size > stride || (long)count * stride > bytes.Length)
            throw new ArgumentException("Invalid mesh channel layout.");
        for (int i = 0; i < count; i++)
        {
            int p = checked(i * stride + offset);
            if (format == 0)
            {
                float value = BitConverter.ToSingle(bytes, p);
                if (!float.IsFinite(value)) throw new ArgumentException("Mesh contains a non-finite geometry channel.");
                bytes[p + 3] ^= 0x80; // IEEE-754 sign bit; leaves all precision bits intact.
            }
            else if (format == 1)
            {
                ushort bits = BitConverter.ToUInt16(bytes, p);
                if ((bits & 0x7c00) == 0x7c00) throw new ArgumentException("Mesh contains a non-finite half channel.");
                bytes[p + 1] ^= 0x80;
            }
            else if (format == 3)
                bytes[p] = unchecked((byte)(sbyte)(bytes[p] == 128 ? 127 : -(sbyte)bytes[p]));
            else
            {
                short value = BitConverter.ToInt16(bytes, p);
                short mirrored = value == short.MinValue ? short.MaxValue : (short)-value;
                bytes[p] = (byte)mirrored; bytes[p + 1] = (byte)(mirrored >> 8);
            }
        }
    }

    internal static void ReverseTriangles(byte[] bytes, int start, int count, int indexSize)
    {
        if ((indexSize != 2 && indexSize != 4) || start < 0 || count < 0 || count % 3 != 0 ||
            (long)(start + (long)count) * indexSize > bytes.Length)
            throw new ArgumentException("Invalid triangle index range.");
        for (int i = start; i < start + count; i += 3)
            for (int b = 0; b < indexSize; b++)
            {
                int a = (i + 1) * indexSize + b, c = (i + 2) * indexSize + b;
                (bytes[a], bytes[c]) = (bytes[c], bytes[a]);
            }
    }
}
