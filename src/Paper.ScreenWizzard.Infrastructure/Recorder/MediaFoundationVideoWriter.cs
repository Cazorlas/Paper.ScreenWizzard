using System.Runtime.InteropServices;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using SharpGen.Runtime;
using Vortice.MediaFoundation;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>
/// An MP4 written by the Media Foundation Sink Writer (ADR 0004): H.264 from BGRA pictures, AAC from 16-bit PCM. The container is
/// named by an attribute, not by the file's extension, because the file is written as <c>.mp4.part</c> and renamed only once the
/// writer finished cleanly (SPEC recorder F3). Every failure comes back as a result, never an exception. Decides nothing: the times,
/// the sizes and when to stop are the use case's.
/// </summary>
public sealed class MediaFoundationVideoWriter : IVideoWriter, IDisposable
{
    // Media Foundation needs MFStartup once in the process before any other call; its answer is kept, and a failure means no encoder.
    private static readonly Lazy<int> _started = new(() => MediaFactory.MFStartup(true).Code);

    private const uint InterlaceProgressive = 2;
    private const int AacBytesPerSecond = 24_000; // 192 kbit/s

    private IMFSinkWriter? _writer;
    private int _videoStream;
    private int _soundStream = -1;
    private PixelSize _size;
    private string? _path;

    public VideoWriterResult Open(string path, PixelSize size, int framesPerSecond, bool withSound)
    {
        Abandon();
        if (_started.Value < 0)
        {
            return new VideoWriterResult(VideoWriterIssue.Encoder, Hex(_started.Value) + ": Media Foundation could not start");
        }

        try
        {
            _path = path;
            _size = size;
            using var attributes = MediaFactory.MFCreateAttributes(2);
            attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1u);
            attributes.Set(TranscodeAttributeKeys.TranscodeContainertype, TranscodeContainerTypeGuids.Mpeg4);
            _writer = MediaFactory.MFCreateSinkWriterFromURL(path, null!, attributes);

            using (var output = MediaFactory.MFCreateMediaType())
            using (var input = MediaFactory.MFCreateMediaType())
            {
                SetVideo(output, VideoFormatGuids.H264, size, framesPerSecond);
                output.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)Bitrate(size, framesPerSecond));
                SetVideo(input, VideoFormatGuids.Rgb32, size, framesPerSecond);
                _videoStream = _writer.AddStream(output);
                _writer.SetInputMediaType(_videoStream, input, null!);
            }

            _soundStream = -1;
            if (withSound)
            {
                using var output = MediaFactory.MFCreateMediaType();
                using var input = MediaFactory.MFCreateMediaType();
                SetSound(output, AudioFormatGuids.Aac, AacBytesPerSecond);
                SetSound(input, AudioFormatGuids.Pcm, RecorderRules.SampleRate * RecorderRules.Channels * 2);
                input.Set(MediaTypeAttributeKeys.AudioBlockAlignment, (uint)(RecorderRules.Channels * 2));
                input.Set(MediaTypeAttributeKeys.AllSamplesIndependent, 1u);
                _soundStream = _writer.AddStream(output);
                _writer.SetInputMediaType(_soundStream, input, null!);
            }

            _writer.BeginWriting();
            return VideoWriterResult.Ok;
        }
        catch (SharpGenException exception)
        {
            var issue = IsDisk(exception.HResult) ? VideoWriterIssue.Disk : VideoWriterIssue.Encoder;
            Abandon();
            return new VideoWriterResult(issue, Hex(exception.HResult) + ": " + exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Abandon();
            return new VideoWriterResult(VideoWriterIssue.Disk, exception.Message);
        }
    }

    public VideoWriterResult WriteVideo(PixelImage frame, TimeSpan at, TimeSpan duration)
    {
        if (_writer is null)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, "the video is not open");
        }

        var stride = _size.Width * 4;
        var length = stride * _size.Height;
        return Write(_videoStream, length, at, duration, pointer =>
        {
            // RGB32 in Media Foundation is bottom-up: the last row of the picture is the first in the buffer.
            for (var row = 0; row < _size.Height; row++)
            {
                Marshal.Copy(frame.Bgra, row * frame.Width * 4, pointer + ((_size.Height - 1 - row) * stride), stride);
            }
        });
    }

    public VideoWriterResult WriteSound(float[] samples, TimeSpan at)
    {
        if (_writer is null || _soundStream < 0)
        {
            return VideoWriterResult.Ok;
        }

        var pcm = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            pcm[i] = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
        }

        var frames = samples.Length / RecorderRules.Channels;
        var duration = TimeSpan.FromTicks(frames * TimeSpan.TicksPerSecond / RecorderRules.SampleRate);
        return Write(_soundStream, pcm.Length * 2, at, duration, pointer => Marshal.Copy(pcm, 0, pointer, pcm.Length));
    }

    public VideoWriterResult Finish(string finalPath)
    {
        if (_writer is not { } writer || _path is not { } path)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, "the video is not open");
        }

        try
        {
            writer.Finalize();
            writer.Dispose();
            _writer = null;
            File.Move(path, finalPath, overwrite: false);
            _path = null;
            return new VideoWriterResult(VideoWriterIssue.None, null, new FileInfo(finalPath).Length);
        }
        catch (SharpGenException exception)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, Hex(exception.HResult) + ": " + exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, exception.Message);
        }
    }

    public void Abandon()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (SharpGenException)
        {
            // Releasing a writer that failed may fail too; the file is removed either way.
        }

        _writer = null;
        if (_path is { } path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file Windows still holds cannot be removed now; it keeps its .part name, so it is never taken for a video.
            }
        }

        _path = null;
    }

    public void Dispose() => Abandon();

    private VideoWriterResult Write(int stream, int length, TimeSpan at, TimeSpan duration, Action<IntPtr> fill)
    {
        try
        {
            using var buffer = MediaFactory.MFCreateMemoryBuffer(length);
            buffer.Lock(out var pointer, out _, out _);
            try
            {
                fill(pointer);
            }
            finally
            {
                buffer.Unlock();
            }

            buffer.CurrentLength = length;
            using var sample = MediaFactory.MFCreateSample();
            sample.AddBuffer(buffer);
            sample.SampleTime = at.Ticks;
            sample.SampleDuration = duration.Ticks;
            _writer!.WriteSample(stream, sample);
            return VideoWriterResult.Ok;
        }
        catch (SharpGenException exception)
        {
            return new VideoWriterResult(VideoWriterIssue.Disk, Hex(exception.HResult) + ": " + exception.Message);
        }
    }

    private static void SetVideo(IMFMediaType type, Guid subtype, PixelSize size, int framesPerSecond)
    {
        type.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        type.Set(MediaTypeAttributeKeys.Subtype, subtype);
        type.Set(MediaTypeAttributeKeys.InterlaceMode, InterlaceProgressive);
        type.Set(MediaTypeAttributeKeys.FrameSize, Pack((uint)size.Width, (uint)size.Height));
        type.Set(MediaTypeAttributeKeys.FrameRate, Pack((uint)framesPerSecond, 1));
        type.Set(MediaTypeAttributeKeys.PixelAspectRatio, Pack(1, 1));
    }

    private static void SetSound(IMFMediaType type, Guid subtype, int bytesPerSecond)
    {
        type.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        type.Set(MediaTypeAttributeKeys.Subtype, subtype);
        type.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16u);
        type.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, (uint)RecorderRules.SampleRate);
        type.Set(MediaTypeAttributeKeys.AudioNumChannels, (uint)RecorderRules.Channels);
        type.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, (uint)bytesPerSecond);
    }

    // About 0.1 bit a pixel a frame: 1920 × 1080 at 30 fps is about 6 Mbit/s, which keeps 10 minutes near the 300 MB of SPEC recorder.
    private static long Bitrate(PixelSize size, int framesPerSecond) =>
        Math.Clamp((long)size.Width * size.Height * framesPerSecond / 10, 1_000_000L, 40_000_000L);

    private static ulong Pack(uint high, uint low) => ((ulong)high << 32) | low;

    // E_ACCESSDENIED, ERROR_PATH_NOT_FOUND, ERROR_DISK_FULL, ERROR_HANDLE_DISK_FULL, ERROR_WRITE_PROTECT, ERROR_SHARING_VIOLATION.
    private static bool IsDisk(int hresult) =>
        hresult is unchecked((int)0x80070005) or unchecked((int)0x80070003) or unchecked((int)0x80070070) or unchecked((int)0x80070027)
            or unchecked((int)0x80070013) or unchecked((int)0x80070020);

    private static string Hex(int hresult) => "0x" + hresult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
}
