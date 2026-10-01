using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PowerlineTool.Core;

namespace PowerlineTool;

/// <summary>Tiny localisation helper: Croatian when the Windows UI language (or settings) says so, English otherwise.</summary>
static class L
{
    public static string Override = "auto"; // auto | en | hr
    static bool Hr => Override == "hr" || (Override != "en" && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "hr");
    public static string T(string en, string hr) => Hr ? hr : en;
    public static string F(string en, string hr, params object[] args) => string.Format(Hr ? hr : en, args);
}

static class Theme
{
    public const string Font = "Segoe UI";
    public static bool Dark;

    public static Color Teal, TealDark, TealText, Header, Bg, Card, Border, Text, Muted, Good, Warn, Bad, Track, Accent2, InputBg;

    static Theme() => Apply(false);

    public static void Apply(bool dark)
    {
        Dark = dark;
        if (!dark)
        {
            Teal = Color.FromArgb(0x1B, 0xA9, 0xB8); TealDark = Color.FromArgb(0x0E, 0x7C, 0x8A); TealText = TealDark;
            Header = TealDark;
            Bg = Color.FromArgb(0xF2, 0xF5, 0xF7); Card = Color.White; Border = Color.FromArgb(0xDD, 0xE3, 0xE8);
            Text = Color.FromArgb(0x22, 0x2B, 0x33); Muted = Color.FromArgb(0x6E, 0x7C, 0x88);
            Good = Color.FromArgb(0x2E, 0xB8, 0x72); Warn = Color.FromArgb(0xF2, 0xA9, 0x1F); Bad = Color.FromArgb(0xE5, 0x4B, 0x4B);
            Track = Color.FromArgb(0xE9, 0xEE, 0xF1); Accent2 = Color.FromArgb(0x7C, 0x5C, 0xE0); InputBg = Color.White;
        }
        else
        {
            Teal = Color.FromArgb(0x2C, 0xC4, 0xD2); TealDark = Color.FromArgb(0x0E, 0x7C, 0x8A); TealText = Color.FromArgb(0x4F, 0xD8, 0xE4);
            Header = Color.FromArgb(0x0B, 0x4F, 0x58);
            Bg = Color.FromArgb(0x12, 0x17, 0x1C); Card = Color.FromArgb(0x1B, 0x22, 0x2A); Border = Color.FromArgb(0x2A, 0x34, 0x3E);
            Text = Color.FromArgb(0xE6, 0xED, 0xF3); Muted = Color.FromArgb(0x93, 0xA1, 0xAD);
            Good = Color.FromArgb(0x3D, 0xD6, 0x8C); Warn = Color.FromArgb(0xF5, 0xB8, 0x3D); Bad = Color.FromArgb(0xFF, 0x6B, 0x6B);
            Track = Color.FromArgb(0x28, 0x31, 0x3A); Accent2 = Color.FromArgb(0xA3, 0x8B, 0xF5); InputBg = Color.FromArgb(0x23, 0x2C, 0x35);
        }
    }

    public static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static bool Resolve(string setting) => setting switch { "dark" => true, "light" => false, _ => SystemIsDark() };

    public static Color RateColor(int mbps) => Quality.Of(mbps) switch
    {
        LinkQuality.Poor => Bad,
        LinkQuality.Fair => Warn,
        _ => Good,
    };

    public static string QualityLabel(LinkQuality q) => q switch
    {
        LinkQuality.Excellent => L.T("Excellent", "Odlično"),
        LinkQuality.Good => L.T("Good", "Dobro"),
        LinkQuality.Fair => L.T("Fair", "Prihvatljivo"),
        _ => L.T("Poor", "Slabo"),
    };

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dark title bar on Windows 10 20H1+ / 11. Silently does nothing where unsupported.</summary>
    public static void TitleBar(Form f)
    {
        try { int v = Dark ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int)); } catch { }
    }
}
