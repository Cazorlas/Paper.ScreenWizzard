using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>
/// The computer's sound (WASAPI loopback of the default playback device) and the microphone (the default recording device), through
/// NAudio (ADR 0004). Each block is stamped on the recorder's clock at the moment its first sample was heard, and converted to 48 kHz
/// stereo floats (<see cref="SoundConverter"/>). A device that is missing is said at start; one that goes away while recording is
/// <see cref="Lost"/>. Loopback hands nothing while the computer is silent: the recorder's mixer counts that as silence.
/// </summary>
public sealed class WasapiSoundSources : ISoundSources, IDisposable
{
    private readonly IMonotonicClock _clock;
    private readonly List<Running> _running = [];
    private readonly object _lock = new();

    public WasapiSoundSources(IMonotonicClock clock)
    {
        _clock = clock;
    }

    public event Action<SoundSource, TimeSpan, float[]>? Samples;

    public event Action<SoundSource, string>? Lost;

    public SoundStartResult Start(bool systemSound, bool microphone)
    {
        Stop();
        string? systemDetail = null;
        string? microphoneDetail = null;
        var systemStarted = systemSound && TryStart(SoundSource.System, () => new WasapiLoopbackCapture(), out systemDetail);
        var microphoneStarted = microphone && TryStart(SoundSource.Microphone, OpenMicrophone, out microphoneDetail);
        return new SoundStartResult(systemStarted, microphoneStarted, systemDetail, microphoneDetail);
    }

    public void Stop()
    {
        List<Running> stopping;
        lock (_lock)
        {
            stopping = [.. _running];
            _running.Clear();
        }

        foreach (var running in stopping)
        {
            running.Stopping = true;
            try
            {
                running.Capture.StopRecording();
            }
            catch (Exception exception) when (exception is COMException or InvalidOperationException)
            {
                // A device already gone cannot be stopped; it is released below either way.
            }

            running.Capture.Dispose();
        }
    }

    public void Dispose() => Stop();

    private static WasapiCapture OpenMicrophone()
    {
        using var devices = new MMDeviceEnumerator();
        if (!devices.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console))
        {
            throw new InvalidOperationException("No microphone is connected.");
        }

        return new WasapiCapture(devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console));
    }

    private bool TryStart(SoundSource source, Func<WasapiCapture> open, out string? detail)
    {
        detail = null;
        WasapiCapture? capture = null;
        try
        {
            capture = open();
            var format = capture.WaveFormat;
            var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
                || (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32 && IsFloatExtensible(format));
            var running = new Running(source, capture, new SoundConverter(format.SampleRate, format.Channels, format.BitsPerSample, isFloat));
            capture.DataAvailable += (_, e) => OnData(running, e);
            capture.RecordingStopped += (_, e) => OnStopped(running, e);
            capture.StartRecording();
            lock (_lock)
            {
                _running.Add(running);
            }

            return true;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            capture?.Dispose();
            detail = exception.Message;
            return false;
        }
    }

    private void OnData(Running running, WaveInEventArgs e)
    {
        if (running.Stopping || e.BytesRecorded == 0)
        {
            return;
        }

        // The block was heard over its own length, ending now.
        var at = _clock.Now - running.Converter.Duration(e.BytesRecorded);
        var samples = running.Converter.Convert(e.Buffer, e.BytesRecorded);
        if (samples.Length > 0)
        {
            Samples?.Invoke(running.Source, at, samples);
        }
    }

    private void OnStopped(Running running, StoppedEventArgs e)
    {
        if (running.Stopping)
        {
            return;
        }

        lock (_lock)
        {
            _running.Remove(running);
        }

        Lost?.Invoke(running.Source, e.Exception?.Message ?? "the device stopped");
    }

    // WAVE_FORMAT_EXTENSIBLE with 32 bits is float on every shared-mode mix format Windows gives; integer 32-bit is told by its sub-format.
    private static bool IsFloatExtensible(WaveFormat format) =>
        format is not WaveFormatExtensible extensible || extensible.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");

    private sealed class Running(SoundSource source, WasapiCapture capture, SoundConverter converter)
    {
        public SoundSource Source { get; } = source;

        public WasapiCapture Capture { get; } = capture;

        public SoundConverter Converter { get; } = converter;

        public volatile bool Stopping;
    }
}
