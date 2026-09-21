using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>How the canvas was left, as text: where its panels stand, how wide they were pulled, what is folded away,
/// and - when the switches say so - where the camera was looking and what was in hand. Handed over rather than saved
/// here: where it is kept is the application's business.
/// <para>A panel's place is a fraction of its travel, so another window size brings it back where it belongs; its
/// width is in pixels, held down to what the window can show.</para></summary>
public static class CanvasLayoutSerializer
{
    public const int Version = 1;

    /// <summary>What the canvas is set up like now, as text. Null for no canvas.</summary>
    public static string Save(InfiniteCanvas canvas)
    {
        if (canvas == null) return null;

        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);

            writer.WriteStartObject("remembers");
            writer.WriteBoolean("panes", canvas.RemembersPanes);
            writer.WriteBoolean("camera", canvas.RemembersCamera);
            writer.WriteBoolean("zoom", canvas.RemembersZoom);
            writer.WriteBoolean("tool", canvas.RemembersTool);
            writer.WriteEndObject();

            if (canvas.RemembersPanes) WritePanes(writer, canvas);

            // The middle of the view in WORLD units, and each half only if it is wanted: what is not remembered is
            // not written down.
            if (canvas.RemembersCamera || canvas.RemembersZoom)
            {
                writer.WriteStartObject("camera");

                if (canvas.RemembersCamera)
                {
                    writer.WriteNumber("x", canvas.Looking.X);
                    writer.WriteNumber("y", canvas.Looking.Y);
                }

                if (canvas.RemembersZoom) writer.WriteNumber("scale", canvas.Scale);

                writer.WriteEndObject();
            }

            Where(writer, "home", canvas.HomeAt, canvas.HomeScale);

            if (canvas.RemembersTool && canvas.Tool != null)
                writer.WriteString("tool", canvas.Tool.GetType().Name);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Puts a canvas back the way that text says. False for text this version cannot read, having changed
    /// nothing.</summary>
    public static bool Load(InfiniteCanvas canvas, string text)
    {
        if (canvas == null || string.IsNullOrWhiteSpace(text)) return false;

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

            if (root.TryGetProperty("remembers", out var remembers) && remembers.ValueKind == JsonValueKind.Object)
            {
                canvas.RemembersPanes = Flag(remembers, "panes", canvas.RemembersPanes);
                canvas.RemembersCamera = Flag(remembers, "camera", canvas.RemembersCamera);
                canvas.RemembersZoom = Flag(remembers, "zoom", canvas.RemembersZoom);
                canvas.RemembersTool = Flag(remembers, "tool", canvas.RemembersTool);
            }

            if (canvas.RemembersPanes) ReadPanes(root, canvas);

            if (root.TryGetProperty("home", out var home) && Read(home, out var homeAt, out var homeScale))
            {
                canvas.HomeAt = homeAt;
                canvas.HomeScale = homeScale;
            }

            // The zoom first: centring uses the scale, so a camera put down before it lands at the wrong place.
            if (root.TryGetProperty("camera", out var camera) && camera.ValueKind == JsonValueKind.Object)
            {
                if (canvas.RemembersZoom && camera.TryGetProperty("scale", out _))
                    canvas.Scale = Number(camera, "scale", canvas.Scale);

                if (canvas.RemembersCamera && camera.TryGetProperty("x", out _))
                    canvas.Look(new Vector2(Number(camera, "x", 0), Number(camera, "y", 0)));
            }

            if (canvas.RemembersTool && root.TryGetProperty("tool", out var tool)
                && tool.ValueKind == JsonValueKind.String)
            {
                Reach(canvas, tool.GetString());
            }

            return true;
        }
    }

    private static void WritePanes(Utf8JsonWriter writer, InfiniteCanvas canvas)
    {
        writer.WriteStartArray("panes");

        foreach (var pane in canvas.Panes)
        {
            // Named or nothing: a pane is found again by the name its template gave it.
            if (pane == null || string.IsNullOrEmpty(pane.Name)) continue;

            writer.WriteStartObject();
            writer.WriteString("name", pane.Name);
            writer.WriteString("placement", pane.Placement.ToString());
            writer.WriteNumber("x", pane.Anchor.X);
            writer.WriteNumber("y", pane.Anchor.Y);

            if (!Double.IsNaN(pane.Width)) writer.WriteNumber("width", pane.Width);
            if (!Double.IsNaN(pane.Height)) writer.WriteNumber("height", pane.Height);

            writer.WriteBoolean("open", pane.IsOpen);
            writer.WriteBoolean("wanted", pane.IsWanted);
            writer.WriteBoolean("snaps", pane.SnapsToEdges);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void ReadPanes(JsonElement root, InfiniteCanvas canvas)
    {
        if (!root.TryGetProperty("panes", out var panes) || panes.ValueKind != JsonValueKind.Array) return;

        foreach (var said in panes.EnumerateArray())
        {
            if (said.ValueKind != JsonValueKind.Object) continue;
            if (!said.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) continue;

            var pane = Named(canvas, name.GetString());
            if (pane == null) continue;

            if (said.TryGetProperty("placement", out var placement) && placement.ValueKind == JsonValueKind.String
                && Enum.TryParse<CanvasPanePlacement>(placement.GetString(), out var where))
            {
                pane.Placement = where;
            }

            pane.Anchor = new Vector2(Number(said, "x", pane.Anchor.X), Number(said, "y", pane.Anchor.Y));

            if (said.TryGetProperty("width", out var wide) && wide.ValueKind == JsonValueKind.Number)
                pane.Width = Math.Max(pane.MinResizeWidth, wide.GetDouble());

            if (said.TryGetProperty("height", out var tall) && tall.ValueKind == JsonValueKind.Number)
                pane.Height = tall.GetDouble();

            pane.IsOpen = Flag(said, "open", pane.IsOpen);
            pane.IsWanted = Flag(said, "wanted", pane.IsWanted);
            pane.SnapsToEdges = Flag(said, "snaps", pane.SnapsToEdges);
        }
    }

    private static CanvasPane Named(InfiniteCanvas canvas, string name)
    {
        foreach (var pane in canvas.Panes)
        {
            if (pane != null && string.Equals(pane.Name, name, StringComparison.Ordinal)) return pane;
        }

        return null;
    }

    private static void Reach(InfiniteCanvas canvas, string named)
    {
        if (canvas.Tools == null || string.IsNullOrEmpty(named)) return;

        foreach (var tool in canvas.Tools)
        {
            if (tool != null && string.Equals(tool.GetType().Name, named, StringComparison.Ordinal))
            {
                canvas.Tool = tool;
                return;
            }
        }
    }

    private static void Where(Utf8JsonWriter writer, string what, Vector2 at, double scale)
    {
        writer.WriteStartObject(what);
        writer.WriteNumber("x", at.X);
        writer.WriteNumber("y", at.Y);
        writer.WriteNumber("scale", scale);
        writer.WriteEndObject();
    }

    private static bool Read(JsonElement said, out Vector2 at, out double scale)
    {
        at = Vector2.Zero;
        scale = 1;

        if (said.ValueKind != JsonValueKind.Object) return false;

        at = new Vector2(Number(said, "x", 0), Number(said, "y", 0));
        scale = Number(said, "scale", 1);
        return true;
    }

    private static double Number(JsonElement said, string what, double or) =>
        said.TryGetProperty(what, out var found) && found.ValueKind == JsonValueKind.Number
            ? found.GetDouble()
            : or;

    private static bool Flag(JsonElement said, string what, bool or) =>
        said.TryGetProperty(what, out var found) && found.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? found.GetBoolean()
            : or;
}
