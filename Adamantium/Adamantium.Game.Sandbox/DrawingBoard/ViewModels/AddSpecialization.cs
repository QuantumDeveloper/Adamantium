using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>COLOURS ADDED, the way lights on one surface add - as many as arrive.</summary>
public sealed class AddSpecialization : NodeSpecialization
{
    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        double r = 0, g = 0, b = 0, a = 0;

        foreach (var color in Paints(inputs))
        {
            r += color.R;
            g += color.G;
            b += color.B;
            a = color.A;
        }

        return new(Made(r, g, b, a));
    }
}
