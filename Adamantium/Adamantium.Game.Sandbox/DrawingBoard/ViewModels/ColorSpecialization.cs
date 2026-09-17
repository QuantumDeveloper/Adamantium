using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>A COLOUR somebody picked - where colour enters the graph.</summary>
public partial class ColorSpecialization : NodeSpecialization
{
    [Bindable] private Color _color = Color.FromRgba(80, 140, 220, 255);

    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token) =>
        new(Color);
}
