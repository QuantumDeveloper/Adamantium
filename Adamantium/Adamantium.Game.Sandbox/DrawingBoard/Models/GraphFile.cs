using System.Collections.Generic;
using System.Text.Json;
using Adamantium.Core.Collections;
using Adamantium.Game.Sandbox.DrawingBoard.ViewModels;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>The graph as TEXT, written and read by the page that owns it.
/// <para>The engine has no part in this and should not: a graph is the application's own objects, and only the
/// application knows what they are made of. Nodes are numbered so a wire can name its two ends; what a node carries is
/// written under its kind, which is also what says how to read it back.</para></summary>
public static class GraphFile
{
    private const int Version = 3;

    private sealed class Saved
    {
        public int Version { get; set; }

        /// <summary>WHICH CATALOGUE this graph was made with. A file read back with somebody else's kinds finds nothing
        /// by those words and comes back as a plane of blank nodes, which reads as a broken file rather than as the
        /// wrong one - so the set is written down and checked.</summary>
        public string Set { get; set; } = string.Empty;

        public List<SavedNode> Nodes { get; set; } = new();
        public List<SavedWire> Wires { get; set; } = new();
    }

    private sealed class SavedNode
    {
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public bool Collapsed { get; set; }

        /// <summary>The color a person gave it, as R, G, B, A - empty when it wears the theme's.</summary>
        public int[] Accent { get; set; }
        public List<SavedSocket> In { get; set; } = new();
        public List<SavedSocket> Out { get; set; } = new();
        public string State { get; set; } = string.Empty;
    }

    private sealed class SavedSocket
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;

        /// <summary>How many wires it takes - a person can change it per socket, so it is theirs and gets written down.
        /// </summary>
        public int Takes { get; set; } = 1;
    }

    private sealed class SavedWire
    {
        public int From { get; set; }
        public string FromSocket { get; set; } = string.Empty;
        public int To { get; set; }
        public string ToSocket { get; set; } = string.Empty;
    }

    /// <summary>Writes the graph out. The nodes FIRST and numbered, because a wire can only be written once both its
    /// ends have names. The wires are found by walking the outputs - each one is met exactly once, from the end it
    /// leaves.</summary>
    public static string Write(IReadOnlyList<ICanvasNode> nodes, GraphNodeSet set)
    {
        var saved = new Saved { Version = Version, Set = set?.Name ?? string.Empty };
        var numbers = new Dictionary<ICanvasNode, int>();

        foreach (var node in nodes)
        {
            numbers[node] = saved.Nodes.Count;
            saved.Nodes.Add(Describe(node));
        }

        foreach (var node in nodes)
        {
            foreach (var socket in node.Outputs)
            {
                foreach (var wire in socket.Connections)
                {
                    if (wire.ToNode == null || !numbers.TryGetValue(node, out var from)) continue;
                    if (!numbers.TryGetValue(wire.ToNode, out var to)) continue;

                    saved.Wires.Add(new SavedWire
                    {
                        From = from,
                        FromSocket = socket.Name ?? string.Empty,
                        To = to,
                        ToSocket = wire.To?.Name ?? string.Empty
                    });
                }
            }
        }

        return JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Reads one back with the given catalogue, replacing what is there. Says what stopped it and changes
    /// nothing when it cannot: text this version does not understand, or a graph made with another set of kinds.
    /// </summary>
    public static string Read(string text, TrackingCollection<ICanvasNode> nodes, GraphNodeSet set)
    {
        Saved saved;
        try
        {
            saved = JsonSerializer.Deserialize<Saved>(text);
        }
        catch (JsonException)
        {
            return "that file is not a graph this version can read";
        }

        if (saved == null || saved.Version != Version) return "that file is not a graph this version can read";

        // THE WRONG CATALOGUE is not a broken file, and saying so is the difference between "this needs the shading
        // nodes" and a plane of nodes that are blank for no visible reason.
        if (!string.IsNullOrEmpty(saved.Set) && set != null && saved.Set != set.Name)
        {
            return $"that graph is made of {saved.Set} nodes, and this page is holding {set.Name}";
        }

        nodes.Clear();

        var made = new List<ICanvasNode>(saved.Nodes.Count);

        foreach (var written in saved.Nodes)
        {
            var node = new CanvasNodeViewModel
            {
                Kind = written.Kind,
                Title = written.Title,
                Left = written.Left,
                Top = written.Top,
                Width = written.Width,
                IsCollapsed = written.Collapsed,
                // The file's own color, and the kind's when the file has none - a graph written before colors existed
                // comes back looking like a new one rather than grey.
                Accent = Paint(written.Accent) ?? set?.Named(written.Kind)?.Accent,

                // The catalogue makes the inside, the same as a pick from the palette does, and the file's own state is
                // read into it. Sockets come with it and are then replaced by the ones written down, which carry names
                // a person may have changed.
                Specialization = State(written, set)
            };

            node.Inputs.Clear();
            node.Outputs.Clear();

            foreach (var socket in written.In)
            {
                node.Inputs.Add(new CanvasSocketViewModel(socket.Takes) { Name = socket.Name, Kind = socket.Kind });
            }

            foreach (var socket in written.Out)
            {
                node.Outputs.Add(new CanvasSocketViewModel(socket.Takes) { Name = socket.Name, Kind = socket.Kind });
            }

            made.Add(node);
            nodes.Add(node);
        }

        foreach (var written in saved.Wires)
        {
            if (written.From < 0 || written.From >= made.Count) continue;
            if (written.To < 0 || written.To >= made.Count) continue;

            if (Named(made[written.From].Outputs, written.FromSocket) is not { } from) continue;
            if (Named(made[written.To].Inputs, written.ToSocket) is not { } to) continue;

            // Made is joined: the wire seats itself in both sockets, and there is nothing to add it to afterwards.
            _ = new CanvasConnection(from, to);
        }

        return null;
    }

    private static SavedNode Describe(ICanvasNode node)
    {
        var written = new SavedNode
        {
            Kind = node.Kind ?? string.Empty,
            Title = node.Title ?? string.Empty,
            Left = node.Left,
            Top = node.Top,
            Width = node.Width,
            Collapsed = node.IsCollapsed,
            Accent = node.Accent is { } paint ? [paint.R, paint.G, paint.B, paint.A] : null,
            State = node.Specialization is { } state
                ? JsonSerializer.Serialize(state, state.GetType(), ColorJson.Options)
                : string.Empty
        };

        foreach (var socket in node.Inputs)
        {
            written.In.Add(new SavedSocket
            {
                Name = socket.Name ?? string.Empty,
                Kind = socket.Kind ?? string.Empty,
                Takes = socket.Capacity
            });
        }

        foreach (var socket in node.Outputs)
        {
            written.Out.Add(new SavedSocket
            {
                Name = socket.Name ?? string.Empty,
                Kind = socket.Kind ?? string.Empty,
                Takes = socket.Capacity
            });
        }

        return written;
    }

    // A state is text, and text can only be read into a TYPE - which the kind is what names. A state that does not fit
    // what this version says that kind is gets left behind: the node comes back fresh rather than half-old.
    private static ICanvasNodeSpecialization State(SavedNode written, GraphNodeSet set)
    {
        if (set?.Named(written.Kind)?.Create() is not NodeSpecialization fresh) return null;
        if (string.IsNullOrEmpty(written.State)) return fresh;

        try
        {
            if (JsonSerializer.Deserialize(written.State, fresh.GetType(), ColorJson.Options) is not NodeSpecialization read)
            {
                return fresh;
            }

            // The SHAPE is this version's, not the file's - the file carries what a person set, and what sockets a Mix
            // has is what the catalogue says today.
            read.In = fresh.In;
            read.Out = fresh.Out;
            read.Takes = fresh.Takes;

            return read;
        }
        catch (JsonException)
        {
            return fresh;
        }
    }

    // Null for a node that was never colored - which is what wears the theme's accent.
    private static Adamantium.Mathematics.Color? Paint(int[] written) =>
        written is { Length: 4 }
            ? Adamantium.Mathematics.Color.FromRgba((byte)written[0], (byte)written[1], (byte)written[2],
                (byte)written[3])
            : null;

    private static ICanvasSocket Named(IReadOnlyList<ICanvasSocket> sockets, string name)
    {
        foreach (var socket in sockets)
        {
            if (socket.Name == name) return socket;
        }

        return null;
    }
}
