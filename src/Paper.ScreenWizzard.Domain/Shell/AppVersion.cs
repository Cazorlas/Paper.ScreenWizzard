using System.Globalization;

namespace Paper.ScreenWizzard.Domain.Shell;

/// <summary>A released version of the app, x.y.z as a release is tagged (v0.1.3) and as the exe carries it (0.1.3, 0.1.3.0, 0.1.3+abc).</summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch) : IComparable<AppVersion>
{
    /// <summary>
    /// Reads "0.1.3", "v0.1.3", "0.1.3.0" (a fourth part of 0 only) and "0.1.3+build". A pre-release ("0.2.0-beta") is not a version the
    /// app offers, so it is refused like any other text.
    /// </summary>
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim();
        if (core.StartsWith('v') || core.StartsWith('V'))
        {
            core = core[1..];
        }

        var plus = core.IndexOf('+');
        if (plus >= 0)
        {
            core = core[..plus];
        }

        var parts = core.Split('.');
        if (parts.Length is < 3 or > 4 || (parts.Length == 4 && parts[3] != "0"))
        {
            return false;
        }

        var numbers = new int[3];
        for (var index = 0; index < 3; index++)
        {
            // Digits only: int.TryParse alone would also take "+1", " 1" or "1_000" in some cultures.
            if (parts[index].Length == 0 || !parts[index].All(char.IsAsciiDigit)
                || !int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return false;
            }
        }

        version = new AppVersion(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    public int CompareTo(AppVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
}
