using Elsa.Studio.Branding;
using Elsa.Studio.Branding.Models;
using Microsoft.AspNetCore.Components;

namespace ElsaStudio.Branding;

/// <summary>
/// Branding values supplied by the server (appsettings "Branding" section, passed through getClientConfig in _Host.cshtml).
/// Empty values fall back to the Elsa defaults.
/// </summary>
public class ClientBrandingOptions
{
    public string? AppName { get; set; }
    public string? AppTagline { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoReverseUrl { get; set; }
    public string? SidebarLogoUrl { get; set; }
    public string? Favicon16Url { get; set; }
    public string? Favicon32Url { get; set; }
    public string? AppleTouchIconUrl { get; set; }
    public string? LoginBackgroundUrl { get; set; }
    public string? LoginBackgroundReverseUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? DarkPrimaryColor { get; set; }
    public bool ShowElsaLinks { get; set; }
}

/// <summary>
/// Holds the branding options. Registered before the host is built and filled from the client config afterwards.
/// </summary>
public class ClientBranding
{
    public ClientBrandingOptions Options { get; set; } = new();
}

/// <summary>
/// Replaces the Elsa Studio name, tagline, logos and app bar links with the client's branding.
/// </summary>
#pragma warning disable CS0672, CS0618 // AppName and logo members are marked obsolete in favor of a custom component, but are still what the shell and login page use.
public class ClientBrandingProvider(ClientBranding branding) : DefaultBrandingProvider
{
    private ClientBrandingOptions Options => branding.Options;

    public override string AppName => Or(Options.AppName, base.AppName);
    public override string AppTagline => Or(Options.AppTagline, base.AppTagline);
    public override string LogoUrl => Or(Options.LogoUrl, base.LogoUrl);
    public override string LogoReverseUrl => Or(Options.LogoReverseUrl, Or(Options.LogoUrl, base.LogoReverseUrl));
    public override string Favicon16Url => Or(Options.Favicon16Url, base.Favicon16Url);
    public override string Favicon32Url => Or(Options.Favicon32Url, base.Favicon32Url);
    public override string AppleTouchIconUrl => Or(Options.AppleTouchIconUrl, base.AppleTouchIconUrl);

    /// <summary>
    /// With a wide sidebar logo configured, the sidebar shows it (instead of the small square logo and "Name version").
    /// </summary>
    public override RenderFragment Branding => string.IsNullOrWhiteSpace(Options.SidebarLogoUrl)
        ? base.Branding
        : builder =>
        {
            builder.OpenComponent<SidebarBranding>(0);
            builder.AddAttribute(1, nameof(SidebarBranding.LogoUrl), Options.SidebarLogoUrl);
            builder.AddAttribute(2, nameof(SidebarBranding.AppName), AppName);
            builder.CloseComponent();
        };

    public override LoginBranding Login => new()
    {
        BackgroundUrl = Or(Options.LoginBackgroundUrl, base.Login.BackgroundUrl),
        BackgroundReverseUrl = Or(Options.LoginBackgroundReverseUrl, Or(Options.LoginBackgroundUrl, base.Login.BackgroundReverseUrl))
    };

    public override DefaultAppBarIcons AppBarIcons => new()
    {
        ShowDocumentationLink = Options.ShowElsaLinks,
        ShowGitHubLink = Options.ShowElsaLinks
    };

    internal static string Or(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
#pragma warning restore CS0672, CS0618
