using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using NexIsland.Settings;

namespace NexIsland.Services
{
    public class ThemePreset
    {
        public string Name { get; set; } = string.Empty;
        public ThemeStyle Style { get; set; }
        public string BackgroundHex { get; set; } = "#0D0D0E";
        public string ForegroundHex { get; set; } = "#F3F4F6";
        public string AccentHex { get; set; } = "#3B82F6";
        public string CardBackgroundHex { get; set; } = "#1E1E22";
        public string BorderHex { get; set; } = "#2C2C32";
        public double Opacity { get; set; } = 0.95;
    }

    public class ThemeService
    {
        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        public readonly List<ThemePreset> Presets = new()
        {
            new ThemePreset { Name = "Obsidian Glass (Default)", Style = ThemeStyle.Dark, BackgroundHex = "#0C0C0E", ForegroundHex = "#F9FAFB", AccentHex = "#3B82F6", CardBackgroundHex = "#18181D", BorderHex = "#282830", Opacity = 0.96 },
            new ThemePreset { Name = "Cyber Cyan", Style = ThemeStyle.CustomAccent, BackgroundHex = "#070B10", ForegroundHex = "#F0FDFA", AccentHex = "#06B6D4", CardBackgroundHex = "#0E1823", BorderHex = "#162838", Opacity = 0.95 },
            new ThemePreset { Name = "Crimson Sunset", Style = ThemeStyle.CustomAccent, BackgroundHex = "#0F0A0D", ForegroundHex = "#FFF1F2", AccentHex = "#F43F5E", CardBackgroundHex = "#1D1219", BorderHex = "#301B27", Opacity = 0.95 },
            new ThemePreset { Name = "Aurora Green", Style = ThemeStyle.CustomAccent, BackgroundHex = "#080F0A", ForegroundHex = "#F0FDF4", AccentHex = "#10B981", CardBackgroundHex = "#102016", BorderHex = "#1C3626", Opacity = 0.95 },
            new ThemePreset { Name = "Light Frost", Style = ThemeStyle.Light, BackgroundHex = "#F3F4F6", ForegroundHex = "#111827", AccentHex = "#2563EB", CardBackgroundHex = "#FFFFFF", BorderHex = "#E5E7EB", Opacity = 0.97 }
        };

        public event Action? ThemeApplied;

        public void ApplyTheme(AppSettings settings)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var bg = ParseBrush("#0C0C0E");
                var fg = ParseBrush("#F9FAFB");
                var cardBg = ParseBrush("#18181D");
                var border = ParseBrush("#282830");
                var accent = ParseBrush(settings.AccentColorHex);

                if (settings.Theme == ThemeStyle.Light)
                {
                    bg = ParseBrush("#F3F4F6");
                    fg = ParseBrush("#111827");
                    cardBg = ParseBrush("#FFFFFF");
                    border = ParseBrush("#E5E7EB");
                }
                else if (settings.Theme == ThemeStyle.Translucent)
                {
                    bg = new SolidColorBrush(Color.FromArgb((byte)(settings.OpacityLevel * 200), 12, 12, 14));
                }

                app.Resources["IslandBackgroundBrush"] = bg;
                app.Resources["IslandForegroundBrush"] = fg;
                app.Resources["IslandCardBrush"] = cardBg;
                app.Resources["IslandBorderBrush"] = border;
                app.Resources["IslandAccentBrush"] = accent;
                app.Resources["IslandCornerRadius"] = new CornerRadius(settings.CornerRadius);

                ThemeApplied?.Invoke();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to apply theme", ex);
            }
        }

        private static SolidColorBrush ParseBrush(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Black;
            }
        }
    }
}
