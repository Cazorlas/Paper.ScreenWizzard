using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.UseCases.Recorder.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>The computer's sound and the microphone, each as interleaved stereo floats at 48 kHz.</summary>
public interface ISoundSources
{
    /// <summary>Starts the asked sources; one that is missing is said in the result and the other still starts (F4, F5).</summary>
    SoundStartResult Start(bool systemSound, bool microphone);

    void Stop();

    /// <summary>Samples of one source and when they were taken, on the clock of <see cref="IMonotonicClock"/>; raised on the source's thread.</summary>
    event Action<SoundSource, TimeSpan, float[]>? Samples;

    /// <summary>A source went away while recording (a microphone unplugged, F4).</summary>
    event Action<SoundSource, string>? Lost;
}
