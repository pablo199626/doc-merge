using System.Reflection;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf.Tests;

public sealed class CommercialBrandingTests
{
    [Fact]
    public void AssemblyMetadata_DesktopProduct_UsesTalosCommercialIdentity()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;

        Assert.Equal("Talos", assembly.GetName().Name);
        Assert.Equal(new Version(2, 1, 0, 0), assembly.GetName().Version);
        Assert.Equal(
            "Talos",
            assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.Equal(
            "Talos — Automatización documental",
            assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal(
            "Talos — Automatización documental",
            assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description);
        Assert.Equal(
            "1.0.0",
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        Assert.Null(assembly.GetCustomAttribute<AssemblyCompanyAttribute>());
    }
}
