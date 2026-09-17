using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>WHAT A NODE IS - the part of a node that a change of kind replaces, while the node itself stays the object
/// every wire and the selection already point at.
/// <para>It is three things at once and deliberately: it SHAPES the node (which sockets it has and what they carry), it
/// is the node's CONTENT (the body template is picked for its type and binds to its properties), and it is what the
/// node COMPUTES. The last one is why this graph is not a picture of a graph: a value arrives along the wires, each
/// node does its bit, and the result comes out at the end.</para>
/// <para>A VIEW-MODEL, and said the way every other one here is said: the notification side is not written by hand at
/// all - what a person sets is a <c>[Bindable]</c> field and the generator writes the property. The derived kinds need
/// it too: the generator writes a property for a field only when the class it is in already has somewhere to raise the
/// change from.</para></summary>
[ViewModel]
public abstract partial class NodeSpecialization : ICanvasNodeSpecialization, ICanvasNodeWork
{
    /// <summary>What the sockets down each side are called and what flows through them. Not saved - it is what the kind
    /// MEANS, and a file that carried it would pin down what this version says a Mix is.</summary>
    [JsonIgnore]
    public IReadOnlyList<(string Name, string Kind)> In { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<(string Name, string Kind)> Out { get; set; } = [];

    /// <summary>How many wires each input takes; zero for as many as come, which is what a merge is.</summary>
    [JsonIgnore]
    public int Takes { get; set; } = 1;

    public void Shape(ICanvasNode node)
    {
        Fit(node.Inputs, In, Takes);
        Fit(node.Outputs, Out, CanvasNodeViewModel.Branches);
    }

    // The node's own sockets brought level with what this kind is made of - names and kinds included, because what a
    // socket CARRIES is what decides whether a wire may go on it and what colour it wears.
    private static void Fit(TrackingCollection<ICanvasSocket> sockets,
        IReadOnlyList<(string Name, string Kind)> wanted, int takes)
    {
        while (sockets.Count > wanted.Count) sockets.RemoveAt(sockets.Count - 1);

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < sockets.Count)
            {
                sockets[i].Name = wanted[i].Name;
                sockets[i].Kind = wanted[i].Kind;
                sockets[i].Capacity = takes;
                continue;
            }

            sockets.Add(new CanvasSocketViewModel(takes) { Name = wanted[i].Name, Kind = wanted[i].Kind });
        }
    }

    /// <summary>WHAT IT COMPUTES, from what arrived on its sockets - each socket's values, with what that socket
    /// carries. Read BY KIND and not by position: a node then means the same thing whether its colours come in on the
    /// two sockets its kind gave it or on a third somebody added, and a socket that takes several wires does not shift
    /// the ones after it.</summary>
    public abstract ValueTask<object> Evaluate(IReadOnlyList<CanvasArrival> inputs, CancellationToken token);

    /// <summary>Every COLOUR that arrived, in socket order - all the wires of all the colour sockets.</summary>
    protected static List<Color> Paints(IReadOnlyList<CanvasArrival> inputs)
    {
        var colors = new List<Color>();

        foreach (var arrival in inputs)
        {
            foreach (var value in arrival.Values)
            {
                if (value is Color color) colors.Add(color);
            }
        }

        return colors;
    }

    /// <summary>The first NUMBER that arrived, or zero - an amount is one thing, however many sockets offer one.
    /// </summary>
    protected static double Number(IReadOnlyList<CanvasArrival> inputs)
    {
        foreach (var arrival in inputs)
        {
            foreach (var value in arrival.Values)
            {
                if (value is double number) return number;
            }
        }

        return 0;
    }

    /// <summary>The colours as ONE colour, averaged - what a node with one colour to work on does when it is handed
    /// several.</summary>
    protected static Color Paint(IReadOnlyList<CanvasArrival> inputs)
    {
        var colors = Paints(inputs);
        if (colors.Count == 0) return Color.FromRgba(0, 0, 0, 255);

        double r = 0, g = 0, b = 0, a = 0;
        foreach (var color in colors)
        {
            r += color.R;
            g += color.G;
            b += color.B;
            a += color.A;
        }

        return Made(r / colors.Count, g / colors.Count, b / colors.Count, a / colors.Count);
    }

    protected static Color Made(double r, double g, double b, double a = 255) =>
        Color.FromRgba((byte)Clamped(r), (byte)Clamped(g), (byte)Clamped(b), (byte)Clamped(a));

    private static double Clamped(double value) => value < 0 ? 0 : value > 255 ? 255 : value;
}
