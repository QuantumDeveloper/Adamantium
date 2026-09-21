using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UITests.Graph;

/// <summary>A NODE THAT COMPUTES, as a test has to supply one: what a node works out is the application's, so the
/// simplest arithmetic that can be checked stands in for it - its own number plus every number that arrived.</summary>
public class GraphWork : ICanvasNodeSpecialization, ICanvasNodeWork, INotifyPropertyChanged
{
    private double _value;

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>What it is worth before anything arrives - the leaf case, and what a test changes to move the graph.
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value)) return;

            _value = value;
            Told();
        }
    }

    /// <summary>How many times it has actually been asked. The whole point of a runner is that this does not go up for
    /// nodes nothing happened to.</summary>
    public int Ran { get; private set; }

    /// <summary>Something to wait on, ONCE - for catching a pass in flight. One-shot deliberately: the pass that
    /// follows a cancelled one must be free to finish, or the test hangs on its own trap.</summary>
    public Func<CancellationToken, Task> WaitsOnce { get; set; }

    public void Shape(ICanvasNode node)
    {
    }

    public async ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token)
    {
        Ran++;

        var waits = WaitsOnce;
        WaitsOnce = null;

        if (waits != null) await waits(token);

        var total = Value;

        foreach (var arrival in inputs)
        {
            foreach (var value in arrival.Values)
            {
                if (value is double number) total += number;
            }
        }

        return total;
    }

    protected void Told([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
