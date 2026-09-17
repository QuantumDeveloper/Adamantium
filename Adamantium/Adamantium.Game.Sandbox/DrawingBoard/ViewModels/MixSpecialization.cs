using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>COLOURS BLENDED by an amount: nothing of the next at zero, all of it at one. Two is the usual case and the
/// kind gives it two sockets - but a third colour blends in the same way, one step at a time, so a socket somebody adds
/// does what the node is for rather than nothing at all.</summary>
public sealed class MixSpecialization : NodeSpecialization
{
    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        var colors = Paints(inputs);
        if (colors.Count == 0) return new(Made(0, 0, 0));

        var amount = Number(inputs);
        var blended = colors[0];

        for (var i = 1; i < colors.Count; i++)
        {
            var next = colors[i];

            blended = Made(
                blended.R + (next.R - blended.R) * amount,
                blended.G + (next.G - blended.G) * amount,
                blended.B + (next.B - blended.B) * amount,
                blended.A + (next.A - blended.A) * amount);
        }

        return new(blended);
    }
}
