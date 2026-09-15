using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>What the application has to provide for a node to come back as itself.
/// <para>The engine writes and reads the SHAPE of a graph - which nodes there are, where they sit, what their sockets
/// are called and which of them are joined. What a node MEANS is not its business and never can be: "Multiply" is a
/// word in somebody's application.</para>
/// <para>So a node carries a <see cref="Kind"/> and a <see cref="Payload"/> that the engine round-trips without once
/// looking inside, and on loading it asks the application to make the node. An application that does not answer gets
/// the plain node the file describes - which is why a graph saved by a program you do not have still opens and is still
/// legible.</para></summary>
public sealed class CanvasNodeSeed
{
    /// <summary>What the application calls this sort of node. Empty for a node nobody claimed.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Whatever the application needs to make this node again, as text it chose the shape of. Never read by
    /// the engine.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>What the node says at the top.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Where and how big, in WORLD units - never screen pixels, which mean nothing at another zoom.</summary>
    public Rect World { get; init; }

    public IReadOnlyList<CanvasSocketSeed> Inputs { get; init; } = Array.Empty<CanvasSocketSeed>();

    public IReadOnlyList<CanvasSocketSeed> Outputs { get; init; } = Array.Empty<CanvasSocketSeed>();
}

/// <summary>One socket of a saved node.</summary>
public sealed class CanvasSocketSeed
{
    public string Name { get; init; } = string.Empty;

    /// <summary>The socket's own colour as "#AARRGGBB", or empty to take the node's.</summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>Reads and writes the GRAPH on a canvas as text - JSON, indented, with a version at the top, for the same
/// reason <see cref="Docking.DockingLayoutSerializer"/> has one: a file outlives the code that wrote it, and the first
/// thing a future reader needs is permission to say "I do not know this one".
/// <para>Nodes are named by an ID that exists only inside the file, and wires refer to a node and a socket BY NAME.
/// Never by index: a node deleted between one save and the next would silently re-point every wire after it at its
/// neighbour, and a drawing that loads wrong is worse than one that refuses to.</para>
/// <para>The CAMERA is saved with the graph. A plane has no edges, and a big graph opened at the origin with the work
/// three screens away reads as an empty document.</para></summary>
public static class CanvasGraphSerializer
{
    public const int Version = 1;

    /// <summary>Writes the graph the canvas is showing - the items of <see cref="CanvasMode.Nodes"/> and nothing else,
    /// so a scene that also holds a drawing gives up only its graph.</summary>
    /// <param name="describe">What the application knows about a node that the engine does not: its kind and its
    /// payload. Not given, every node is saved as the plain shape it is.</param>
    public static string Save(InfiniteCanvas canvas, Func<CanvasNode, (string Kind, string Payload)> describe = null)
    {
        if (canvas == null) return null;

        // The nodes FIRST and numbered, because a wire can only be written once both its ends have names.
        var ids = new Dictionary<ElementItem, int>();
        var nodes = new List<(int Id, ElementItem Item, CanvasNode Node)>();
        var wires = new List<ConnectionItem>();

        foreach (var item in canvas.ItemsHere())
        {
            switch (item)
            {
                case ElementItem element when element.Element is CanvasNode node:
                    ids[element] = nodes.Count;
                    nodes.Add((nodes.Count, element, node));
                    break;

                case ConnectionItem wire:
                    wires.Add(wire);
                    break;
            }
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);

            WriteCamera(writer, canvas);

            writer.WriteStartArray("nodes");
            foreach (var (id, item, node) in nodes) WriteNode(writer, id, item, node, describe);
            writer.WriteEndArray();

            writer.WriteStartArray("links");
            foreach (var wire in wires) WriteLink(writer, wire, ids);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Reads a graph back onto the canvas: the nodes it describes, the wires between them, and the camera it
    /// was left at. Returns false for text this version cannot read, having changed nothing.
    /// <para>What is already on the plane of the other mode is untouched - a graph is loaded INTO a canvas, not over
    /// it. The graph that was there is replaced, because two graphs in one place is not a thing a file can mean.</para>
    /// </summary>
    /// <param name="make">Asked for each node, with the kind and payload the file recorded. Returning null - or not
    /// being given at all - leaves the engine to make a plain node from the shape.</param>
    public static bool Load(InfiniteCanvas canvas, string text, Func<CanvasNodeSeed, CanvasNode> make = null)
    {
        if (canvas?.Scene is not { } scene || string.IsNullOrWhiteSpace(text)) return false;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("version", out var version) || version.GetInt32() > Version) return false;

            // EVERYTHING READ BEFORE ANYTHING IS CHANGED: a file that turns out to be broken half way through must
            // leave the canvas as it found it, not half a graph.
            var seeds = ReadNodes(root);
            var links = ReadLinks(root);

            foreach (var item in new List<ICanvasItem>(canvas.ItemsHere())) scene.Remove(item);

            var placed = new List<ElementItem>(seeds.Count);
            foreach (var seed in seeds)
            {
                var node = make?.Invoke(seed) ?? Plain(seed);
                Dress(node, seed);

                var element = new ElementItem(node, seed.World);
                scene.Add(element);
                placed.Add(element);
            }

            foreach (var (from, fromPin, to, toPin) in links)
            {
                if (from < 0 || from >= placed.Count || to < 0 || to >= placed.Count) continue;

                var wire = Join(placed[from], fromPin, placed[to], toPin);
                if (wire != null) scene.Add(wire);
            }

            ReadCamera(root, canvas);
            scene.Touch();
        }

        return true;
    }

    private static void WriteCamera(Utf8JsonWriter writer, InfiniteCanvas canvas)
    {
        writer.WriteStartObject("camera");
        writer.WriteNumber("x", canvas.Offset.X);
        writer.WriteNumber("y", canvas.Offset.Y);
        writer.WriteNumber("scale", canvas.Scale);
        writer.WriteEndObject();
    }

    private static void ReadCamera(JsonElement root, InfiniteCanvas canvas)
    {
        if (!root.TryGetProperty("camera", out var camera) || camera.ValueKind != JsonValueKind.Object) return;

        // The SCALE first: the offset is what the camera is pointed at, and setting it at the old zoom would put the
        // graph somewhere else and then zoom about that.
        if (camera.TryGetProperty("scale", out var scale) && scale.TryGetDouble(out var zoom) && zoom > 0)
            canvas.SetCurrentValue(InfiniteCanvas.ScaleProperty, zoom);

        if (camera.TryGetProperty("x", out var x) && camera.TryGetProperty("y", out var y) &&
            x.TryGetDouble(out var left) && y.TryGetDouble(out var top))
        {
            canvas.SetCurrentValue(InfiniteCanvas.OffsetProperty, new Vector2(left, top));
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, int id, ElementItem item, CanvasNode node,
        Func<CanvasNode, (string Kind, string Payload)> describe)
    {
        var said = describe?.Invoke(node) ?? (string.Empty, string.Empty);

        writer.WriteStartObject();
        writer.WriteNumber("id", id);
        if (!string.IsNullOrEmpty(said.Kind)) writer.WriteString("kind", said.Kind);
        if (!string.IsNullOrEmpty(said.Payload)) writer.WriteString("payload", said.Payload);
        writer.WriteString("title", node.Title?.ToString() ?? string.Empty);

        writer.WriteStartObject("world");
        writer.WriteNumber("x", item.World.X);
        writer.WriteNumber("y", item.World.Y);
        writer.WriteNumber("width", item.World.Width);
        writer.WriteNumber("height", item.World.Height);
        writer.WriteEndObject();

        WriteSockets(writer, "in", node.InputPins);
        WriteSockets(writer, "out", node.OutputPins);

        writer.WriteEndObject();
    }

    private static void WriteSockets(Utf8JsonWriter writer, string name, IEnumerable<CanvasNodePin> pins)
    {
        writer.WriteStartArray(name);
        foreach (var pin in pins)
        {
            writer.WriteStartObject();
            writer.WriteString("name", pin.Name ?? string.Empty);

            var colour = Hex(pin.Color);
            if (!string.IsNullOrEmpty(colour)) writer.WriteString("color", colour);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteLink(Utf8JsonWriter writer, ConnectionItem wire, Dictionary<ElementItem, int> ids)
    {
        if (wire.FromItem == null || wire.ToItem == null) return;
        if (!ids.TryGetValue(wire.FromItem, out var from) || !ids.TryGetValue(wire.ToItem, out var to)) return;

        writer.WriteStartObject();
        writer.WriteNumber("from", from);
        writer.WriteString("fromSocket", wire.FromPin?.Name ?? string.Empty);
        writer.WriteNumber("to", to);
        writer.WriteString("toSocket", wire.ToPin?.Name ?? string.Empty);
        writer.WriteEndObject();
    }

    private static List<CanvasNodeSeed> ReadNodes(JsonElement root)
    {
        var seeds = new List<CanvasNodeSeed>();
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return seeds;

        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object) continue;

            seeds.Add(new CanvasNodeSeed
            {
                Kind = Text(node, "kind"),
                Payload = Text(node, "payload"),
                Title = Text(node, "title"),
                World = Box(node),
                Inputs = Sockets(node, "in"),
                Outputs = Sockets(node, "out")
            });
        }

        return seeds;
    }

    private static List<(int From, string FromPin, int To, string ToPin)> ReadLinks(JsonElement root)
    {
        var links = new List<(int, string, int, string)>();
        if (!root.TryGetProperty("links", out var array) || array.ValueKind != JsonValueKind.Array) return links;

        foreach (var link in array.EnumerateArray())
        {
            if (link.ValueKind != JsonValueKind.Object) continue;
            if (!link.TryGetProperty("from", out var from) || !from.TryGetInt32(out var fromId)) continue;
            if (!link.TryGetProperty("to", out var to) || !to.TryGetInt32(out var toId)) continue;

            links.Add((fromId, Text(link, "fromSocket"), toId, Text(link, "toSocket")));
        }

        return links;
    }

    private static List<CanvasSocketSeed> Sockets(JsonElement node, string name)
    {
        var sockets = new List<CanvasSocketSeed>();
        if (!node.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return sockets;

        foreach (var socket in array.EnumerateArray())
        {
            if (socket.ValueKind != JsonValueKind.Object) continue;

            sockets.Add(new CanvasSocketSeed { Name = Text(socket, "name"), Color = Text(socket, "color") });
        }

        return sockets;
    }

    private static CanvasNode Plain(CanvasNodeSeed seed) => new();

    // The shape the file recorded, put onto whatever node came back - the application's or the engine's. Sockets it
    // already has KEEP their names and colours only where the file had nothing to say, so a node the application built
    // with its own sockets is not flattened by being loaded.
    private static void Dress(CanvasNode node, CanvasNodeSeed seed)
    {
        if (!string.IsNullOrEmpty(seed.Title)) node.Title = seed.Title;

        Fit(node.InputPins, seed.Inputs, true);
        Fit(node.OutputPins, seed.Outputs, false);
    }

    private static void Fit(System.Collections.ObjectModel.ObservableCollection<CanvasNodePin> pins,
        IReadOnlyList<CanvasSocketSeed> seeds, bool input)
    {
        if (seeds.Count == 0) return;

        while (pins.Count > seeds.Count) pins.RemoveAt(pins.Count - 1);
        while (pins.Count < seeds.Count) pins.Add(new CanvasNodePin { IsInput = input });

        for (var i = 0; i < seeds.Count; i++)
        {
            pins[i].IsInput = input;
            if (!string.IsNullOrEmpty(seeds[i].Name)) pins[i].Name = seeds[i].Name;

            if (Colour(seeds[i].Color) is { } colour) pins[i].Color = new SolidColorBrush(colour);
        }
    }

    // A wire between two sockets found BY NAME. Nothing is invented: a socket the file names and the node does not have
    // means a wire that is simply not made, the same way a saved pane with no control is dropped rather than conjured.
    private static ConnectionItem Join(ElementItem from, string fromPin, ElementItem to, string toPin)
    {
        if (from.Element is not CanvasNode source || to.Element is not CanvasNode target) return null;

        var leaves = Find(source.OutputPins, fromPin);
        var arrives = Find(target.InputPins, toPin);
        if (leaves == null || arrives == null) return null;

        leaves.IsConnected = true;
        arrives.IsConnected = true;

        return new ConnectionItem(from, leaves, to, arrives);
    }

    private static CanvasNodePin Find(IEnumerable<CanvasNodePin> pins, string name)
    {
        foreach (var pin in pins)
        {
            if (string.Equals(pin.Name, name, StringComparison.Ordinal)) return pin;
        }

        return null;
    }

    private static string Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static Rect Box(JsonElement node)
    {
        if (!node.TryGetProperty("world", out var world) || world.ValueKind != JsonValueKind.Object) return default;

        return new Rect(Number(world, "x"), Number(world, "y"), Number(world, "width"), Number(world, "height"));
    }

    private static double Number(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : 0;

    // INVARIANT hex, because a colour written under one language must read back under another - see the note on
    // markup numbers.
    private static string Hex(Brush brush) =>
        brush is SolidColorBrush solid
            ? "#" + solid.Color.A.ToString("X2", CultureInfo.InvariantCulture)
                  + solid.Color.R.ToString("X2", CultureInfo.InvariantCulture)
                  + solid.Color.G.ToString("X2", CultureInfo.InvariantCulture)
                  + solid.Color.B.ToString("X2", CultureInfo.InvariantCulture)
            : string.Empty;

    private static Color? Colour(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length != 9) return null;

        return byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a) &&
               byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
               byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
               byte.TryParse(hex.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)
            ? new Color(r, g, b, a)
            : null;
    }
}
