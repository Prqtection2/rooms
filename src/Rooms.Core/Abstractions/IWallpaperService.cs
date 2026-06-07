namespace Rooms.Core.Abstractions;

/// <summary>Sets the desktop wallpaper - part of a room's ambient settings (§6.2 step 9).</summary>
public interface IWallpaperService
{
    void SetWallpaper(string imagePath);
}
