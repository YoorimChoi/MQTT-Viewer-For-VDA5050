using MaterialDesignThemes.Wpf;
using MqttViewer.Infrastructure;

namespace MqttViewer.Services;

public sealed class AppThemeManager : ObservableObject
{
    private readonly PaletteHelper _paletteHelper = new();
    private bool _isDarkMode;

    private AppThemeManager()
    {
        ApplyTheme();
    }

    public static AppThemeManager Instance { get; } = new();

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (!SetProperty(ref _isDarkMode, value))
            {
                return;
            }

            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(_isDarkMode ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
    }
}
