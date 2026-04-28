using System.IO.Compression;
using System.Text;

namespace Cisharpai.Tests.Common;

/// <summary>
/// Generates valid PNG images in-memory for testing. Avoids relying on hardcoded
/// minimal PNGs that some vision providers (e.g. Anthropic) reject as too small.
/// </summary>
public static class PngGenerator
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] CreateSolidColorPng(int width, int height, byte r, byte g, byte b)
    {
        using var ms = new MemoryStream();
        ms.Write(PngSignature);

        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr, 0, (uint)width);
        WriteUInt32BigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // color type: RGB
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(ms, "IHDR", ihdr);

        var stride = 1 + width * 3;
        var raw = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0; // filter: None
            for (var x = 0; x < width; x++)
            {
                var pix = rowStart + 1 + x * 3;
                raw[pix] = r;
                raw[pix + 1] = g;
                raw[pix + 2] = b;
            }
        }

        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }
            compressed = compressedStream.ToArray();
        }
        WriteChunk(ms, "IDAT", compressed);
        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var lenBuf = new byte[4];
        WriteUInt32BigEndian(lenBuf, 0, (uint)data.Length);
        stream.Write(lenBuf, 0, 4);

        var typeBuf = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBuf, 0, typeBuf.Length);
        stream.Write(data, 0, data.Length);

        var crcInput = new byte[typeBuf.Length + data.Length];
        Buffer.BlockCopy(typeBuf, 0, crcInput, 0, typeBuf.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBuf.Length, data.Length);
        var crc = Crc32(crcInput);

        var crcBuf = new byte[4];
        WriteUInt32BigEndian(crcBuf, 0, crc);
        stream.Write(crcBuf, 0, 4);
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset]     = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }
        return c ^ 0xFFFFFFFFu;
    }
}
