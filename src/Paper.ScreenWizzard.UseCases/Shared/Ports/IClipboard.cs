using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

public interface IClipboard
{
    /// <summary>Puts the image on the clipboard, retrying while another program holds it (SPEC capture F5).</summary>
    PortResult SetImage(PixelImage image);

    ClipboardImageResult GetImage();
}
