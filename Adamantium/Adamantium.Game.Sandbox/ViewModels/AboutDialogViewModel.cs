using System;
using System.Reflection;
using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>About dialog (IDialogAware): product name, version and manufacturer read from the Sandbox assembly's
/// metadata (Product / InformationalVersion / Company / Copyright, set in the csproj). Closed with OK via RequestClose.</summary>
[ViewModel]
public partial class AboutDialogViewModel : AdamantiumViewModel, IDialogAware
{
    public AboutDialogViewModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();

        Product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? name.Name;
        Manufacturer = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Adamantium";
        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational ?? name.Version?.ToString() ?? "1.0.0";
        var plus = version.IndexOf('+');   // drop the "+<git hash>" SourceLink suffix for display
        Version = plus >= 0 ? version[..plus] : version;
    }

    public string Title => "About";
    public string Product { get; }
    public string Version { get; }
    public string Manufacturer { get; }
    public string Copyright { get; }

    public void OnDialogOpened(NavigationParameters parameters) { }

    public bool CanCloseDialog() => true;

    public event Action<IDialogResult> RequestClose;

    [Command] private void Ok() => RequestClose?.Invoke(DialogResult.Ok());
}
