using System.IO;
using Microsoft.Extensions.Logging;
using Rooms.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rooms.Os.Windows;

/// <summary>Sets the desktop wallpaper via SystemParametersInfo (§6.2 step 9).</summary>
public sealed class Win32WallpaperService : IWallpaperService
{
    private readonly ILogger<Win32WallpaperService> _logger;

    public Win32WallpaperService(ILogger<Win32WallpaperService> logger)
    {
        _logger = logger;
    }

    public unsafe void SetWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            _logger.LogWarning("Wallpaper not set: file not found ({Path}).", imagePath);
            return;
        }

        fixed (char* path = imagePath)
        {
            var ok = PInvoke.SystemParametersInfo(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDESKWALLPAPER,
                0,
                path,
                SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE | SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDWININICHANGE);

            if (!ok)
                _logger.LogWarning("SystemParametersInfo(SPI_SETDESKWALLPAPER) failed for {Path}.", imagePath);
        }
    }
}
