using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>An MP4 being written: H.264 pictures and, when asked, one AAC sound track.</summary>
public interface IVideoWriter
{
    VideoWriterResult Open(string path, PixelSize size, int framesPerSecond, bool withSound);

    VideoWriterResult WriteVideo(PixelImage frame, TimeSpan at, TimeSpan duration);

    /// <summary>Interleaved stereo floats at 48 kHz, starting at <paramref name="at"/> of the video.</summary>
    VideoWriterResult WriteSound(float[] samples, TimeSpan at);

    /// <summary>Completes the file and gives it <paramref name="finalPath"/>.</summary>
    VideoWriterResult Finish(string finalPath);

    /// <summary>Drops the file being written: nothing half-written is left (F3).</summary>
    void Abandon();
}
