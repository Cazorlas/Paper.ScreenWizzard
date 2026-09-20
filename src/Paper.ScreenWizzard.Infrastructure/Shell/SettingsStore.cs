using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// The one settings document, <c>&lt;dataRoot&gt;\configs\settings.json</c>, as indented JSON with enums written as names. A file that
/// cannot be read or is not a valid document is copied over <c>settings.json.bak</c> and reported <see cref="SettingsLoadStatus.Corrupt"/>
/// (SPEC shell F1). A save goes to a temporary file first and then replaces the document, so a failure half way never leaves a torn file;
/// a failed save reports <c>"&lt;full path&gt;: &lt;reason&gt;"</c> so the shell's message can name the file (SPEC shell F2).
/// </summary>
public sealed class SettingsStore : ISettingsStorePort
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private static readonly byte[] _utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly string _folder;
    private readonly string _file;

    public SettingsStore(string dataRoot)
    {
        _folder = Path.Combine(dataRoot, "configs");
        _file = Path.Combine(_folder, "settings.json");
    }

    public SettingsLoadResult Load()
    {
        if (!File.Exists(_file))
        {
            return new SettingsLoadResult(null, SettingsLoadStatus.Missing, null);
        }

        try
        {
            var settings = Read();
            return new SettingsLoadResult(settings, SettingsLoadStatus.Loaded, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            // Locked or denied: the file may be perfectly good, so it is not backed up as "corrupt"; the use case decides what a save may do.
            return new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, _file + ": " + exception.Message);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException)
        {
            KeepAsBackup();
            return new SettingsLoadResult(null, SettingsLoadStatus.Corrupt, _file + ": " + exception.Message);
        }
    }

    // Notepad and PowerShell 5 write a UTF-8 byte order mark; the JSON reader refuses it, and a file that opens fine in an editor is not corrupt.
    private AppSettings Read()
    {
        var bytes = File.ReadAllBytes(_file);
        var content = bytes.AsSpan().StartsWith(_utf8Bom) ? bytes.AsSpan(_utf8Bom.Length) : bytes.AsSpan();
        var document = JsonSerializer.Deserialize<SettingsDocument>(content, _options)
            ?? throw new JsonException("the document is empty");
        return document.ToSettings();
    }

    public PortResult Save(AppSettings settings)
    {
        var temporary = _file + ".tmp";
        try
        {
            Directory.CreateDirectory(_folder);
            var json = JsonSerializer.SerializeToUtf8Bytes(SettingsDocument.From(settings), _options);
            File.WriteAllBytes(temporary, json);
            if (File.Exists(_file))
            {
                File.Replace(temporary, _file, null);
            }
            else
            {
                File.Move(temporary, _file);
            }

            return PortResult.Ok;
        }
        catch (Exception exception)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception)
            {
                // The leftover temporary file is harmless; the reason of the failed save is what matters.
            }

            return PortResult.Fail(_file + ": " + exception.Message);
        }
    }

    // The bad file is kept next to the new one so the user can look at it (SPEC shell F1); an older backup is overwritten.
    private void KeepAsBackup()
    {
        try
        {
            File.Copy(_file, _file + ".bak", overwrite: true);
        }
        catch (Exception)
        {
            // A file that cannot even be copied cannot be kept; the Corrupt answer is still right.
        }
    }

    // The document is its own set of plain types, so a change to the domain record never silently changes the file format.
    private sealed class SettingsDocument
    {
        public required Dictionary<string, HotkeyDocument> Hotkeys { get; init; }

        public required AfterCaptureAction AfterCapture { get; init; }

        public required string SaveFolder { get; init; }

        public required ImageFormat Format { get; init; }

        public required int JpgQuality { get; init; }

        public required int DelaySeconds { get; init; }

        public required bool IncludeCursor { get; init; }

        public required FullScreenScope FullScreenScope { get; init; }

        public required bool StartWithWindows { get; init; }

        public required AppLanguage Language { get; init; }

        public required AppTheme Theme { get; init; }

        public PositionDocument? CaptureBarPosition { get; init; }

        public static SettingsDocument From(AppSettings settings) => new()
        {
            Hotkeys = settings.Hotkeys.ToDictionary(pair => pair.Key.ToString(), pair => new HotkeyDocument(pair.Value.Modifiers, pair.Value.Key)),
            AfterCapture = settings.AfterCapture,
            SaveFolder = settings.SaveFolder,
            Format = settings.Format,
            JpgQuality = settings.JpgQuality,
            DelaySeconds = settings.DelaySeconds,
            IncludeCursor = settings.IncludeCursor,
            FullScreenScope = settings.FullScreenScope,
            StartWithWindows = settings.StartWithWindows,
            Language = settings.Language,
            Theme = settings.Theme,
            CaptureBarPosition = settings.CaptureBarPosition is { } place ? new PositionDocument(place.X, place.Y) : null,
        };

        public AppSettings ToSettings()
        {
            var hotkeys = new Dictionary<CaptureKind, HotkeyChord>();
            // System.Text.Json accepts null for a non-nullable property: a null table or a null chord is a broken document, not a crash.
            foreach (var (name, chord) in Hotkeys ?? throw new JsonException("the hotkeys are missing"))
            {
                if (!Enum.TryParse<CaptureKind>(name, out var kind) || !Enum.IsDefined(kind) || chord is null || string.IsNullOrWhiteSpace(chord.Key))
                {
                    throw new JsonException($"'{name}' is not a capture kind with a key");
                }

                hotkeys[kind] = new HotkeyChord(chord.Modifiers, chord.Key);
            }

            if (string.IsNullOrWhiteSpace(SaveFolder))
            {
                throw new JsonException("the save folder is empty");
            }

            if (JpgQuality is < 1 or > 100)
            {
                throw new JsonException($"the JPG quality {JpgQuality} is not between 1 and 100");
            }

            if (DelaySeconds < 0)
            {
                throw new JsonException($"the delay {DelaySeconds} is negative");
            }

            return new AppSettings(
                hotkeys,
                AfterCapture,
                SaveFolder,
                Format,
                JpgQuality,
                DelaySeconds,
                IncludeCursor,
                FullScreenScope,
                StartWithWindows,
                Language,
                Theme,
                CaptureBarPosition is { } place ? new PixelPoint(place.X, place.Y) : null);
        }
    }

    private sealed record HotkeyDocument(HotkeyModifiers Modifiers, string Key);

    private sealed record PositionDocument(int X, int Y);
}
