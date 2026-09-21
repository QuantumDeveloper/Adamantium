using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;

/// <summary>THE COLOUR WITHOUT ITS COLOUR - weighted the way an eye weighs the three channels, not by thirds.</summary>
public sealed class GraySpecialization : NodeSpecialization
{
    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        var color = Paint(inputs);
        var grey = color.R * 0.299 + color.G * 0.587 + color.B * 0.114;

        return new(Made(grey, grey, grey, color.A));
    }
}
