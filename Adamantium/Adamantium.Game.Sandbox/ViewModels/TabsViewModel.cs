using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Tabs tab: the two things a tab strip has to survive, each with the knob that governs it.
/// <para>A THOUSAND tabs with long titles, against <see cref="IsVirtualizing"/> - on, only the headers on screen are
/// built; off, all thousand are. Measured at 45ms of layout against 2517ms.</para>
/// <para>A DOZEN tabs against <see cref="MaxOpenedTabs"/>, which closes the oldest to make room for a new one.</para>
/// </summary>
[ViewModel]
public partial class TabsViewModel : TabPageViewModel
{
    public TabsViewModel() : base("Tabs") { }

    /// <summary>Long titles on purpose: they are what makes a content-sized strip ragged, and what the tab template has
    /// to trim with an ellipsis once a uniform width stops it from growing to fit.</summary>
    public ObservableCollection<DemoItem> ManyTabs { get; } = new(Enumerable.Range(1, 1000)
        .Select(i => new DemoItem { Name = $"Document {i} - a rather long tab title" }));

    public ObservableCollection<DemoItem> FewTabs { get; } = new(Enumerable.Range(1, 12)
        .Select(i => new DemoItem { Name = $"Report {i}" }));

    [Bindable] private bool _isVirtualizing = true;

    [Bindable] private int _maxOpenedTabs = 5;

    [Bindable] private object _selectedTab;

    /// <summary>Opens a tab and selects it - the whole demonstration: past the cap the OLDEST tab is closed to make room,
    /// and never the one just opened or the one being read.</summary>
    [Command]
    private void AddTab()
    {
        var tab = new DemoItem { Name = $"Report {FewTabs.Count + 1}" };
        FewTabs.Add(tab);
        SelectedTab = tab;
    }
}
