using Avalonia.Media;

namespace Lemon.Template.Avalonia.Services.Theming;

internal static class ThemeColorArgb
{
    public static int Pack(Color c) => (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;

    public static Color Unpack(int argb) =>
        Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}
