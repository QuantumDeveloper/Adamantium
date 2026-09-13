using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Adamantium.MVVM;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

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
    [Bindable] private Color _tint = Colors.CornflowerBlue;

    /// <summary>A BRUSH, not a colour, and the same instance the swatch beside the inspector paints with. The line for
    /// it changes the colour INSIDE it rather than putting a new brush here - which is why the swatch follows without
    /// being told anything: it is holding that brush.</summary>
    public Brush Fill { get; } = new SolidColorBrush(Colors.Tomato);

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

    /// <summary>What the "..." button on a line reports. The command is the APPLICATION'S - the inspector only offers
    /// the button and hands over what the line is pointed at; what "more" means for a tag is the page's business.
    /// </summary>
    [Bindable] private string _actionStatus = "The ... button has not been pressed";

    [Command]
    private void MoreForTag(object target)
    {
        var name = target switch
        {
            InspectedEntity one => one.Name,
            IEnumerable many => string.Join(", ", many.OfType<InspectedEntity>().Select(e => e.Name)),
            _ => "nothing"
        };

        ActionStatus = $"... pressed on the Tag line of: {name}";
    }

    /// <summary>What each inspector is pointed at. DIFFERENT objects by default - the left one at Player, the right one
    /// at Enemy - so both objects' values are on the page at the same time. An inspector over several objects can only
    /// be judged against what each of them holds, and a page showing just one of them makes every row of the multiple
    /// selection an unverifiable claim.</summary>
    public IEnumerable LeftSelection => _both ? new[] { Entity, Other } : new[] { Entity };

    public IEnumerable RightSelection => _both ? new[] { Entity, Other } : new[] { Other };

    [Bindable] private bool _both;

    /// <summary>Whether the generated inspector carries a search field. Bound to a toggle on the page so the property
    /// can be seen doing something: an inspector of a handful of rows does not need one, and the row it takes is worth
    /// more to the rows.</summary>
    [Bindable] private bool _searchable = true;

    public string LeftCaption => _both
        ? "Written by hand - now over BOTH objects"
        : "Written by hand - over Player, sections and properties declared in markup";

    public string RightCaption => _both
        ? "Generated - now over BOTH objects"
        : "Generated - over Enemy, PropertyDefinitionBuilder read the type, no markup at all";

    /// <summary>What the toggle beside it has just done. Spelled out because an inspector over several objects behaves
    /// differently from one over a single object, and a row standing empty is a statement, not a gap.</summary>
    public string Difference => _both
        ? "Both are over Player AND Enemy. Rows the two agree on show that value; only rows they really differ on say 'multiple values' - fill one, both take it."
        : "Two DIFFERENT objects, side by side - compare them. Tick the box to point both inspectors at both at once.";

    partial void OnBothChanged(bool value)
    {
        RaisePropertyChanged(nameof(LeftSelection));
        RaisePropertyChanged(nameof(RightSelection));
        RaisePropertyChanged(nameof(LeftCaption));
        RaisePropertyChanged(nameof(RightCaption));
        RaisePropertyChanged(nameof(Difference));
    }

    /// <summary>The sections the BUILDER makes from the type - no markup at all, which is how an entity's components
    /// will be inspected: it is handed a type and gives back sections carrying ordinary bindings.</summary>
    public IReadOnlyList<PropertySection> Generated { get; } =
        new PropertyDefinitionBuilder().BuildSections(typeof(InspectedEntity));
}
