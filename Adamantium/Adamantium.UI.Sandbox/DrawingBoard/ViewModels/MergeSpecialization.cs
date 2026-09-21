using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;

/// <summary>EVERY COLOUR THAT ARRIVES, averaged - the node whose one input takes as many wires as anybody brings.
/// </summary>
public sealed class MergeSpecialization : NodeSpecialization
{
    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token) =>
        new(Paint(inputs));
}
