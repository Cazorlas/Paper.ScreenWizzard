using Paper.ScreenWizzard.Domain.Recorder;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>
/// Turns what one Windows audio device hands (its own rate, its own channels, float or integer samples) into interleaved stereo floats at
/// 48 kHz, the one format the recorder mixes (SPEC recorder, "Sound"). One converter per device: it keeps where it stopped, so blocks join
/// without a click. Linear interpolation: enough for speech and system sounds; a device at 48 kHz, the usual case, passes straight.
/// </summary>
internal sealed class SoundConverter
{
    private readonly int _rate;
    private readonly int _channels;
    private readonly int _bytesPerSample;
    private readonly bool _float;
    private readonly double _step;
    private double _position;
    private float _lastLeft;
    private float _lastRight;

    public SoundConverter(int rate, int channels, int bitsPerSample, bool isFloat)
    {
        _rate = rate;
        _channels = Math.Max(1, channels);
        _bytesPerSample = bitsPerSample / 8;
        _float = isFloat;
        _step = (double)rate / RecorderRules.SampleRate;
    }

    /// <summary>How long <paramref name="bytes"/> of this device's sound last.</summary>
    public TimeSpan Duration(int bytes) =>
        TimeSpan.FromTicks((long)bytes / (_channels * _bytesPerSample) * TimeSpan.TicksPerSecond / _rate);

    public float[] Convert(byte[] buffer, int bytes)
    {
        var frames = bytes / (_channels * _bytesPerSample);
        if (frames == 0)
        {
            return [];
        }

        // Stereo at the device's rate first: mono is doubled, more than two channels keep the front left and right.
        var left = new float[frames];
        var right = new float[frames];
        for (var f = 0; f < frames; f++)
        {
            var at = f * _channels * _bytesPerSample;
            left[f] = Sample(buffer, at);
            right[f] = _channels > 1 ? Sample(buffer, at + _bytesPerSample) : left[f];
        }

        if (_rate == RecorderRules.SampleRate)
        {
            var same = new float[frames * 2];
            for (var f = 0; f < frames; f++)
            {
                same[2 * f] = left[f];
                same[(2 * f) + 1] = right[f];
            }

            return same;
        }

        // Then to 48 kHz; position 0 is the last sample of the previous block, 1 the first of this one.
        var output = new List<float>((int)(frames / _step) + 4);
        while (_position < frames)
        {
            var index = (int)Math.Floor(_position);
            var fraction = (float)(_position - index);
            var l0 = index == 0 ? _lastLeft : left[index - 1];
            var r0 = index == 0 ? _lastRight : right[index - 1];
            var l1 = left[index];
            var r1 = right[index];
            output.Add(l0 + ((l1 - l0) * fraction));
            output.Add(r0 + ((r1 - r0) * fraction));
            _position += _step;
        }

        _position -= frames;
        _lastLeft = left[frames - 1];
        _lastRight = right[frames - 1];
        return [.. output];
    }

    private float Sample(byte[] buffer, int at) => _bytesPerSample switch
    {
        4 when _float => BitConverter.ToSingle(buffer, at),
        4 => BitConverter.ToInt32(buffer, at) / 2147483648f,
        3 => ((buffer[at] << 8) | (buffer[at + 1] << 16) | (buffer[at + 2] << 24)) / 2147483648f,
        2 => BitConverter.ToInt16(buffer, at) / 32768f,
        8 when _float => (float)BitConverter.ToDouble(buffer, at),
        _ => 0f,
    };
}
