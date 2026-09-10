using System.Collections;
using System.Collections.Generic;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>What the inspector is pointed at: a stand-in for an entity's components, with a property of every kind the
/// first iteration supports.
/// <para>It NOTIFIES, so the two inspectors on the page are live against each other: type into one and the other moves,
/// which is what binding a value properly buys and what reading a member by name never could.</para></summary>
[ViewModel]
public partial class InspectedEntity
{
    [Bindable] private string _name = "Player";
    [Bindable] private bool _isEnabled = true;
    [Bindable] private string _tag = "Character";

    [Bindable] private double _positionX = 12.5;
    [Bindable] private double _positionY;
    [Bindable] private double _positionZ = -4;

    [Bindable] private double _mass = 80;
    [Bindable] private Visibility _visibility = Visibility.Visible;
    [Bindable] private string _material = "Steel";

    public string Guid { get; } = "8f14e45f-ceea-467a-9d3f-1b2c3d4e5f60";
}

/// <summary>Property grid tab: an inspector over one object, with the sections folding and the grip between the halves.
/// <para>Nothing here implements a provider of any kind: what the inspector shows are ordinary bindings, so the list of
/// materials is a property of this model and the row's own <c>ItemsSource</c> binds to it like anything else.</para></summary>
[ViewModel]
public partial class PropertyGridViewModel : TabPageViewModel
{
    public PropertyGridViewModel() : base("Property grid")
    {
    }

    public InspectedEntity Entity { get; } = new();

    /// <summary>A second object, so the inspector can be pointed at BOTH: rows they agree on show the value, rows they
    /// do not say so - and an edit reaches both.</summary>
    public InspectedEntity Other { get; } = new()
    {
        Name = "Enemy",
        Tag = "Character",
        IsEnabled = false,
        Mass = 55,
        Material = "Bone",
        PositionX = -3
    };

    /// <summary>The application's list of materials - not the property's, and not a provider's either.</summary>
    public IReadOnlyList<string> Materials { get; } = ["Steel", "Copper", "Glass", "Rubber", "Bone"];

    /// <summary>What the inspectors are pointed at - one object or two, switched by the toggle below them.</summary>
    public IEnumerable Selection => _both ? new[] { Entity, Other } : new[] { Entity };

    [Bindable] private bool _both;

    partial void OnBothChanged(bool value) => RaisePropertyChanged(nameof(Selection));

    /// <summary>The sections the BUILDER makes from the type - no markup at all, which is how an entity's components
    /// will be inspected: it is handed a type and gives back sections carrying ordinary bindings.</summary>
    public IReadOnlyList<PropertySection> Generated { get; } =
        new PropertyDefinitionBuilder().BuildSections(typeof(InspectedEntity));
}
