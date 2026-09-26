using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Shell;

/// <summary>
/// The one settings document, <c>&lt;dataRoot&gt;\configs\settings.json</c>, as indented JSON with enums written as names. A file that
/// is not a settings document (not JSON, a value of the wrong type) is copied over <c>settings.json.bak</c> and reported
/// <see cref="SettingsLoadStatus.Corrupt"/> (SPEC shell F1); a document is handed over as it is, missing settings as null, and whether
/// its values are in range is the use case's decision (<see cref="KeepAsBackup"/> when they are not). A save goes to a temporary file first and then replaces the document, so a failure half way never leaves a torn file;
/// a failed save reports <c>"&lt;full path&gt;: &lt;reason&gt;"</c> so the shell's message can name the file (SPEC shell F2).
/// </summary>
public sealed class SettingsStore : ISettingsStore
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
            return new SettingsLoadResult(Read(), SettingsLoadStatus.Loaded, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            // Locked or denied: the file may be perfectly good, so it is not backed up as "corrupt"; the use case decides what a save may do.
            return new SettingsLoadResult(null, SettingsLoadStatus.Unreadable, _file + ": " + exception.Message);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException)
        {
            Backup();
            return new SettingsLoadResult(null, SettingsLoadStatus.Corrupt, _file + ": " + exception.Message);
        }
    }

    // Notepad and PowerShell 5 write a UTF-8 byte order mark; the JSON reader refuses it, and a file that opens fine in an editor is not corrupt.
    private StoredSettings Read()
    {
        var bytes = File.ReadAllBytes(_file);
        var content = bytes.AsSpan().StartsWith(_utf8Bom) ? bytes.AsSpan(_utf8Bom.Length) : bytes.AsSpan();
        var document = JsonSerializer.Deserialize<SettingsDocument>(content, _options)
            ?? throw new JsonException("the document is empty");
        return document.ToStored();
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

    public PortResult KeepAsBackup()
    {
        try
        {
            File.Copy(_file, _file + ".bak", overwrite: true);
            return PortResult.Ok;
        }
        catch (Exception exception)
        {
            return PortResult.Fail(_file + ".bak: " + exception.Message);
        }
    }

    // The bad file is kept next to the new one so the user can look at it (SPEC shell F1); an older backup is overwritten. A file that
    // cannot even be copied cannot be kept, and the Corrupt answer is still right.
    private void Backup() => KeepAsBackup();

    // The document is its own set of plain types, so a change to the domain record never silently changes the file format. Every setting
    // may be missing (null): a file of an older version lacks what was added since, and what that means is the use case's call (SPEC
    // shell F8). A setting this version does not know is skipped by the reader, so a file a newer version wrote still loads.
    private sealed class SettingsDocument
    {
        public Dictionary<string, HotkeyDocument?>? Hotkeys { get; init; }

        public AfterCaptureAction? AfterCapture { get; init; }

        public string? SaveFolder { get; init; }

        public ImageFormat? Format { get; init; }

        public int? JpgQuality { get; init; }

        public int? DelaySeconds { get; init; }

        public bool? IncludeCursor { get; init; }

        public FullScreenScope? FullScreenScope { get; init; }

        public bool? StartWithWindows { get; init; }

        public AppLanguage? Language { get; init; }

        public AppTheme? Theme { get; init; }

        public PositionDocument? CaptureBarPosition { get; init; }

        public static SettingsDocument From(AppSettings settings) => new()
        {
            Hotkeys = settings.Hotkeys.ToDictionary(pair => pair.Key.ToString(), pair => (HotkeyDocument?)new HotkeyDocument(pair.Value.Modifiers, pair.Value.Key)),
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

        public StoredSettings ToStored() => new(
            Hotkeys?.ToDictionary(pair => pair.Key, pair => pair.Value is { } chord ? new StoredHotkey(chord.Modifiers, chord.Key) : (StoredHotkey?)null),
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

    private sealed record HotkeyDocument(HotkeyModifiers? Modifiers, string? Key);

    private sealed record PositionDocument(int X, int Y);
}
