using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Markup tab. Shows what the AUML vocabulary gained, and shows it as an A/B where there is one: a value shared
/// between targets against one built per target, and a themed default against an explicit nothing. Pure view: no
/// view-model logic at all - every claim on the page is made by the markup itself.</summary>
[ViewModel]
public partial class MarkupViewModel : TabPageViewModel
{
    /// <summary>Drives the x:Load section. While false the panel below does not exist - not hidden, absent.</summary>
    [Bindable] private bool _heavyShown;

    /// <summary>How many times the held-back panel has been CONSTRUCTED. The whole claim of the section is that this
    /// reads 0 before it is ever shown, and never goes past 1 however often it is toggled.</summary>
    [Bindable] private int _heavyBuilds;

    /// <summary>The same count for the arm nothing watches - the one built only when the button asks for it.</summary>
    [Bindable] private int _manualBuilds;

    public MarkupViewModel() : base("Markup")
    {
        DemoBuildProbe.Built += OnProbeBuilt;
    }

    private void OnProbeBuilt(System.Type probe)
    {
        if (probe == typeof(DemoManualProbe))
        {
            ManualBuilds++;
            return;
        }

        HeavyBuilds++;
    }

    /// <summary>The two rows of each x:Shared arm. Plain strings: the section is about the SETTER, not about the data.</summary>
    public string[] SharedRows { get; } = ["right-click me", "...and me"];

    public string[] OwnRows { get; } = ["right-click me", "...and me"];

    /// <summary>Rows for the x:DataType section - plain data, so the template's declared type is a real one.</summary>
    public MarkupRow[] Rows { get; } =
    [
        new MarkupRow("bound through a template that names its type"),
        new MarkupRow("x:DataType=\"local:ViewModels.MarkupRow\""),
    ];
}
