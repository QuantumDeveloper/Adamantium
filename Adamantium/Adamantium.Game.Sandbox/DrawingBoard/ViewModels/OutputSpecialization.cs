using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>WHERE THE GRAPH ENDS: what arrived, shown as itself. The one node with something to say back to the person
/// who built the graph, which is what makes the whole thing more than a picture.</summary>
public partial class OutputSpecialization : NodeSpecialization
{
    /// <summary>What reached the end. Written by whoever worked the graph out, read by the body template - and not
    /// saved: it is not something a person set, it is what the graph works out.
    /// <para>A partial property rather than a field, because what is said ABOUT it - that a file must not carry it -
    /// belongs on the property, and an attribute on a backing field reaches nothing.</para></summary>
    [JsonIgnore]
    [Bindable]
    public partial Color Result { get; set; }

    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        Result = Paint(inputs);

        return new(Result);
    }
}
