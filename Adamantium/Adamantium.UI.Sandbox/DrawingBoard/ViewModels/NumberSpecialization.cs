using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;

/// <summary>A NUMBER somebody set - an amount, a brightness.</summary>
public partial class NumberSpecialization : NodeSpecialization
{
    [Bindable] private double _value = 0.5;

    public override ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token) =>
        new(Value);
}
