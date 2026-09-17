using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>A COLOUR SCALED by a number - what brightness is. Handed several colours it works on them as one.</summary>
public sealed class ScaleSpecialization : NodeSpecialization
{
    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        var color = Paint(inputs);
        var by = Number(inputs);

        return new(Made(color.R * by, color.G * by, color.B * by, color.A));
    }
}
