using System.Buffers.Binary;
using System.Text;

namespace Paper.ScreenWizzard.E2eTests.Recorder;

/// <summary>
/// Reads back what the test needs from an MP4 without a player: the length (<c>mvhd</c>), the picture size (the video track's
/// <c>tkhd</c>) and whether there is a sound track (a <c>trak</c> whose handler is <c>soun</c>). ISO/IEC 14496-12 boxes: a 32-bit size,
/// a four-letter type, the content; version 1 boxes carry 64-bit times.
/// </summary>
internal sealed record Mp4Probe(TimeSpan Duration, int Width, int Height, bool HasSound)
{
    public static Mp4Probe Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var moov = Find(bytes, 0, bytes.Length, "moov") ?? throw new InvalidDataException("no moov box");
        var mvhd = Find(bytes, moov.Start, moov.End, "mvhd") ?? throw new InvalidDataException("no mvhd box");
        var version = bytes[mvhd.Start];
        long timescale;
        long duration;
        if (version == 1)
        {
            timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd.Start + 20));
            duration = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(mvhd.Start + 24));
        }
        else
        {
            timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd.Start + 12));
            duration = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd.Start + 16));
        }

        var width = 0;
        var height = 0;
        var sound = false;
        foreach (var trak in All(bytes, moov.Start, moov.End, "trak"))
        {
            var mdia = Find(bytes, trak.Start, trak.End, "mdia");
            var hdlr = mdia is null ? null : Find(bytes, mdia.Start, mdia.End, "hdlr");
            var handler = hdlr is null ? string.Empty : Encoding.ASCII.GetString(bytes, hdlr.Start + 8, 4);
            if (handler == "soun")
            {
                sound = true;
            }
            else if (handler == "vide" && Find(bytes, trak.Start, trak.End, "tkhd") is { } tkhd)
            {
                // Width and height are the last two fields of tkhd, 16.16 fixed point.
                width = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(tkhd.End - 8)) >> 16);
                height = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(tkhd.End - 4)) >> 16);
            }
        }

        return new Mp4Probe(TimeSpan.FromSeconds((double)duration / timescale), width, height, sound);
    }

    private static Box? Find(byte[] bytes, int from, int to, string type) => All(bytes, from, to, type).FirstOrDefault();

    private static IEnumerable<Box> All(byte[] bytes, int from, int to, string type)
    {
        var at = from;
        while (at + 8 <= to)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            var name = Encoding.ASCII.GetString(bytes, at + 4, 4);
            var header = 8;
            if (size == 1)
            {
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(at + 8));
                header = 16;
            }
            else if (size == 0)
            {
                size = to - at;
            }

            if (size < header)
            {
                yield break;
            }

            if (name == type)
            {
                yield return new Box(at + header, (int)(at + size));
            }

            at += (int)size;
        }
    }

    private sealed record Box(int Start, int End);
}
