using Elsa.Studio.Contracts;
using Elsa.Studio.Services;
using MudBlazor;

namespace ElsaStudio.Branding;

/// <summary>
/// The client color theme: Elsa's classic theme (cool slate neutrals) with the brand colors from the "Branding" settings
/// (defaults: Dhaka Bank #0C4DA2 navigation blue and #1469DA action blue, taken from dhakabank.com.bd).
/// </summary>
public class ClientThemeProvider(ClientBranding branding) : IThemeProvider
{
    public const string Id = "client";

    public MudTheme GetTheme()
    {
        var options = branding.Options;
        var primary = ClientBrandingProvider.Or(options.PrimaryColor, "#0C4DA2");
        var secondary = ClientBrandingProvider.Or(options.SecondaryColor, "#1469DA");
        var darkPrimary = ClientBrandingProvider.Or(options.DarkPrimaryColor, "#5B9BF0");
        var theme = new ClassicThemeProvider().GetTheme();

        var light = theme.PaletteLight;
        light.Primary = primary;
        light.Secondary = secondary;
        light.Info = secondary;
        light.AppbarBackground = primary;

        // A lighter blue keeps enough contrast on dark backgrounds.
        var dark = theme.PaletteDark;
        dark.Primary = darkPrimary;
        dark.Secondary = darkPrimary;
        dark.Info = darkPrimary;
        dark.AppbarBackground = primary;

        return theme;
    }
}
