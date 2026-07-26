using System.Windows.Media;
using Lemon.Template.Wpf.Services.Theming;
using Xunit;

namespace Lemon.Template.Wpf.Tests;

/// <summary>
/// Theme colours survive a trip through SQLite as a packed int; a regression here silently changes
/// every persisted user theme.
/// </summary>
public class ThemeColorArgbTests
{
    [Theory]
    [InlineData(255, 103, 58, 183)]   // DeepPurple 500, the template default primary
    [InlineData(255, 205, 220, 57)]   // Lime 500, the template default secondary
    [InlineData(0, 0, 0, 0)]
    [InlineData(255, 255, 255, 255)]
    [InlineData(1, 2, 3, 4)]
    public void PackThenUnpack_RoundTripsEveryChannel(byte a, byte r, byte g, byte b)
    {
        var original = Color.FromArgb(a, r, g, b);

        var roundTripped = ThemeColorArgb.Unpack(ThemeColorArgb.Pack(original));

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Pack_PlacesChannelsInArgbOrder()
    {
        var packed = ThemeColorArgb.Pack(Color.FromArgb(0x12, 0x34, 0x56, 0x78));

        Assert.Equal(0x12345678, packed);
    }

    [Fact]
    public void Unpack_KeepsFullyOpaqueAlpha()
    {
        // Sign bit set: alpha 0xFF must not be lost to the int being negative.
        var color = ThemeColorArgb.Unpack(unchecked((int)0xFF203040));

        Assert.Equal(0xFF, color.A);
        Assert.Equal(0x20, color.R);
        Assert.Equal(0x30, color.G);
        Assert.Equal(0x40, color.B);
    }
}
