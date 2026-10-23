namespace Paper.ScreenWizzard.Domain.Recorder;

/// <summary>
/// Mixes the sounds that go into the video (SPEC recorder, "Sound"): each source hands samples with their video time, the mixer adds
/// them at that place and hands back what every source has reached. A source that is silent sends nothing (Windows' loopback does
/// not), so one lagging more than <see cref="MaxLag"/> behind the video counts as silence up to there. Samples are interleaved stereo
/// floats at <see cref="RecorderRules.SampleRate"/>.
/// </summary>
public sealed class AudioMixer
{
    public static readonly TimeSpan MaxLag = TimeSpan.FromMilliseconds(200);

    private readonly int _channels;
    private readonly int _rate;
    private readonly Dictionary<SoundSource, long> _reached = [];
    private float[] _pending = new float[RecorderRules.SampleRate * RecorderRules.Channels];
    private long _written;

    public AudioMixer(IEnumerable<SoundSource> sources, int sampleRate = RecorderRules.SampleRate, int channels = RecorderRules.Channels)
    {
        _rate = sampleRate;
        _channels = channels;
        foreach (var source in sources)
        {
            _reached[source] = 0;
        }
    }

    /// <summary>The video time up to which sound has been handed back.</summary>
    public TimeSpan WrittenTime => TimeOfFrame(_written);

    public bool HasSources => _reached.Count > 0;

    /// <summary>A source that is gone (a microphone taken away, F4) no longer holds the others back.</summary>
    public void Remove(SoundSource source) => _reached.Remove(source);

    public void Push(SoundSource source, TimeSpan mediaTime, ReadOnlySpan<float> interleaved)
    {
        if (!_reached.TryGetValue(source, out var reached))
        {
            return;
        }

        var start = FrameOf(mediaTime);
        var frames = interleaved.Length / _channels;
        var skip = Math.Max(0, _written - start);
        if (skip >= frames)
        {
            return;
        }

        var from = start + skip;
        var end = start + frames;
        Ensure(end);
        var offset = (from - _written) * _channels;
        var samples = interleaved[(int)(skip * _channels)..(frames * _channels)];
        for (var i = 0; i < samples.Length; i++)
        {
            _pending[offset + i] += samples[i];
        }

        _reached[source] = Math.Max(reached, end);
    }

    /// <summary>The mixed sound every source has reached, or that a lagging source is taken to be silent for, up to <paramref name="mediaNow"/>.</summary>
    public float[] Drain(TimeSpan mediaNow)
    {
        var silentUpTo = FrameOf(mediaNow - MaxLag);
        var ready = _reached.Count == 0 ? silentUpTo : _reached.Values.Min(reached => Math.Max(reached, silentUpTo));
        return Take(ready);
    }

    /// <summary>Everything up to <paramref name="videoEnd"/>, silence where nothing came, and nothing after it: the sound ends with the picture.</summary>
    public float[] Flush(TimeSpan videoEnd) => Take(FrameOf(videoEnd));

    private float[] Take(long upTo)
    {
        if (upTo <= _written)
        {
            return [];
        }

        Ensure(upTo);
        var count = (int)((upTo - _written) * _channels);
        var result = new float[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = Math.Clamp(_pending[i], -1f, 1f);
        }

        var left = _pending.Length - count;
        var next = new float[Math.Max(left, _rate * _channels)];
        Array.Copy(_pending, count, next, 0, left);
        _pending = next;
        _written = upTo;
        return result;
    }

    private void Ensure(long upToFrame)
    {
        var needed = (upToFrame - _written) * _channels;
        if (needed <= _pending.Length)
        {
            return;
        }

        var grown = new float[Math.Max(needed, _pending.Length * 2)];
        Array.Copy(_pending, grown, _pending.Length);
        _pending = grown;
    }

    private long FrameOf(TimeSpan time) => time <= TimeSpan.Zero ? 0 : (long)Math.Round(time.TotalSeconds * _rate);

    private TimeSpan TimeOfFrame(long frame) => TimeSpan.FromTicks(frame * TimeSpan.TicksPerSecond / _rate);
}
