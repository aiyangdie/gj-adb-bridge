using System.Drawing;

namespace GjBridge
{
    /// <summary>与网站 sj.aikex.ink 统一的视觉 token（styles.css :root）</summary>
    internal static class Theme
    {
        public static readonly Color Brand = Color.FromArgb(70, 87, 190);       // #4657be
        public static readonly Color BrandDark = Color.FromArgb(52, 68, 158);   // #34449e
        public static readonly Color BrandSoft = Color.FromArgb(227, 231, 250); // #e3e7fa
        public static readonly Color Accent = Color.FromArgb(255, 99, 59);      // #ff633b
        public static readonly Color Bg = Color.FromArgb(245, 246, 247);       // #f5f6f7
        public static readonly Color Surface = Color.White;
        public static readonly Color Ink = Color.FromArgb(24, 43, 55);         // #182b37
        public static readonly Color Muted = Color.FromArgb(104, 123, 134);    // #687b86
        public static readonly Color Line = Color.FromArgb(224, 230, 233);     // #e0e6e9
        public static readonly Color Ok = Color.FromArgb(4, 216, 106);
        public static readonly Color Err = Color.FromArgb(190, 62, 49);
    }
}
