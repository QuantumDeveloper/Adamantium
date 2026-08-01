using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Reads and writes a <see cref="DockingLayout"/> as text. JSON, indented, with a version at the top: a layout outlives
/// the code that wrote it, and the first thing a future reader needs is permission to say "I do not know this one".
/// <para>Panes are named by ID and nothing else - the same reason the model refers to them that way. Loading therefore
/// yields a layout of ids, and it is the AREA that decides which of them it actually has controls for: a saved id whose
/// pane no longer exists is dropped, never invented.</para>
/// <para>What is NOT saved is decided by the caller through <c>keepPane</c>. Tools come back with the workspace;
/// documents belong to a session and may not even exist next time (see <see cref="Pane.Restore"/>).</para>
/// </summary>
public static class DockingLayoutSerializer
{
    public const int Version = 1;

    /// <param name="restoreKeyOf">What the application needs in order to make a pane again (<see cref="Pane.RestoreKey"/>).
    /// Written beside the id, so a pane that was opened by code - and therefore does not exist at start-up - can be
    /// recreated instead of silently dropped.</param>
    public static string Save(DockingLayout layout, Func<string, bool> keepPane = null, Func<string, string> restoreKeyOf = null)
    {
        if (layout == null) return null;

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);

            // id -> what makes it again, for the ones that need making. Written once for the whole layout rather than
            // beside every mention: a pane appears in exactly one group, but the table reads far better than a key
            // buried in a tree, and a future reader can see at a glance what a file expects the application to provide.
            var keys = CollectRestoreKeys(layout, keepPane, restoreKeyOf);
            if (keys.Count > 0)
            {
                writer.WriteStartObject("restore");
                foreach (var pair in keys) writer.WriteString(pair.Key, pair.Value);
                writer.WriteEndObject();
            }

            writer.WriteStartArray("roots");

            foreach (var root in layout.Roots)
            {
                WriteRoot(writer, root, keepPane);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>What a saved layout expects the application to be able to make: pane id -> restore key. Read BEFORE the
    /// layout is applied, so the panes can be brought into being and the arrangement then simply finds them.</summary>
    public static IReadOnlyDictionary<string, string> ReadRestoreKeys(string text)
    {
        var keys = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(text)) return keys;

        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("restore", out var restore)
                || restore.ValueKind != JsonValueKind.Object)
            {
                return keys;
            }

            foreach (var pair in restore.EnumerateObject())
            {
                var key = pair.Value.GetString();
                if (!string.IsNullOrEmpty(key)) keys[pair.Name] = key;
            }
        }
        catch (JsonException)
        {
            // A corrupt file is the caller's problem to report once, on Load - not twice.
        }

        return keys;
    }

    private static Dictionary<string, string> CollectRestoreKeys(DockingLayout layout, Func<string, bool> keep, Func<string, string> keyOf)
    {
        var keys = new Dictionary<string, string>();
        if (keyOf == null) return keys;

        foreach (var root in layout.Roots)
        {
            foreach (var id in DockingLayout.PanesIn(root.Content)) Collect(id);

            foreach (var bar in root.Bars.Values)
            {
                foreach (var group in bar)
                {
                    foreach (var id in group.PaneIds) Collect(id);
                }
            }
        }

        return keys;

        void Collect(string id)
        {
            if (!Keep(id, keep)) return;

            var key = keyOf(id);
            if (!string.IsNullOrEmpty(key)) keys[id] = key;
        }
    }

    /// <summary>Reads a layout back. Returns null for text this version does not understand rather than throwing: a
    /// layout file is user data, and a corrupt one means "start from the authored arrangement", not "do not start".
    /// <para><paramref name="knownPane"/> answers whether a saved id still stands for a pane that exists. Ids it
    /// rejects are dropped, and groups and splits left empty by that go with them - a layout must never name a pane
    /// nobody can produce.</para></summary>
    public static DockingLayout Load(string text, Func<string, bool> knownPane = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (!root.TryGetProperty("version", out var version) || version.GetInt32() > Version) return null;
            if (!root.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array) return null;

            var layout = new DockingLayout();
            foreach (var element in roots.EnumerateArray())
            {
                var read = ReadRoot(element, layout, knownPane);
                if (read != null) layout.Roots.Add(read);
            }

            if (layout.Roots.Count == 0) return null;

            // Dropping ids leaves the same debris any other removal does - a split down to one child, a group down to
            // none - and a loaded layout has to arrive in the shape the rest of the code expects, not in a special one.
            layout.Normalize();
            return layout.Main != null ? layout : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteRoot(Utf8JsonWriter writer, DockingRoot root, Func<string, bool> keep)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("main", root.IsMain);

        // The ONE piece of absolute geometry in a layout, and only worth writing for a window that has one: everything
        // below is fractions, which is what lets a saved layout survive a different screen untouched.
        if (root.Bounds.Width > 0 && root.Bounds.Height > 0)
        {
            writer.WriteStartArray("bounds");
            writer.WriteNumberValue(root.Bounds.X);
            writer.WriteNumberValue(root.Bounds.Y);
            writer.WriteNumberValue(root.Bounds.Width);
            writer.WriteNumberValue(root.Bounds.Height);
            writer.WriteEndArray();
        }

        // The window's OWN document area travels with its tree: which node is the area has to survive the round trip, or
        // the centre stops existing the moment the layout is loaded and the next document opens wherever it likes.
        if (WriteNode(writer, "content", root.DocumentWell, root.Content, keep) == false)
        {
            writer.WriteNull("content");
        }

        // The edge bars are written SEPARATELY from the tree, because that is where they live (rule 3b).
        var wroteBars = false;
        foreach (var bar in root.Bars)
        {
            if (bar.Value.Count == 0) continue;

            if (!wroteBars)
            {
                writer.WriteStartObject("bars");
                wroteBars = true;
            }

            writer.WriteStartArray(bar.Key.ToString());
            foreach (var group in bar.Value) WriteGroup(writer, root.DocumentWell, group, keep);
            writer.WriteEndArray();
        }

        if (wroteBars) writer.WriteEndObject();

        writer.WriteEndObject();
    }

    // Returns false when the node writes nothing at all - every pane in it was dropped by keepPane.
    private static bool WriteNode(Utf8JsonWriter writer, string name, PaneNode well, PaneNode node, Func<string, bool> keep)
    {
        switch (node)
        {
            case PaneGroupNode group when HasKeptPane(group, keep):
                writer.WritePropertyName(name);
                WriteGroup(writer, well, group, keep);
                return true;

            case PaneSplitNode split when HasKeptPane(split, keep):
                writer.WritePropertyName(name);
                WriteSplit(writer, well, split, keep);
                return true;

            default:
                return false;
        }
    }

    private static void WriteSplit(Utf8JsonWriter writer, PaneNode well, PaneSplitNode split, Func<string, bool> keep)
    {
        writer.WriteStartObject();
        writer.WriteString("split", split.Orientation.ToString());
        writer.WriteString("length", split.Length.ToString());

        // A SPLIT can be the document area too, once the area has been divided (rule 1.6). Written only on the group
        // before, a saved split area came back as an ordinary row and its editors as tools.
        if (ReferenceEquals(split, well)) writer.WriteBoolean("well", true);

        writer.WriteStartArray("children");

        foreach (var child in split.Children)
        {
            switch (child)
            {
                case PaneGroupNode group when HasKeptPane(group, keep):
                    WriteGroup(writer, well, group, keep);
                    break;
                case PaneSplitNode nested when HasKeptPane(nested, keep):
                    WriteSplit(writer, well, nested, keep);
                    break;
            }
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteGroup(Utf8JsonWriter writer, PaneNode well, PaneGroupNode group, Func<string, bool> keep)
    {
        writer.WriteStartObject();

        writer.WriteStartArray("panes");
        foreach (var id in group.PaneIds)
        {
            if (Keep(id, keep)) writer.WriteStringValue(id);
        }
        writer.WriteEndArray();

        writer.WriteNumber("active", ActiveAmongKept(group, keep));
        writer.WriteString("length", group.Length.ToString());
        writer.WriteString("restore", group.RestoreLength.ToString());

        if (group.State != PaneGroupState.Docked) writer.WriteString("state", group.State.ToString());

        if (ReferenceEquals(group, well)) writer.WriteBoolean("well", true);

        writer.WriteEndObject();
    }

    private static DockingRoot ReadRoot(JsonElement element, DockingLayout layout, Func<string, bool> known)
    {
        // Which node this window's document area is, collected as the tree is read - it is a node in that tree, so it
        // cannot be named before the tree exists.
        PaneNode well = null;

        var content = element.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Object
            ? ReadNode(contentElement, layout, known, ref well)
            : null;

        var root = new DockingRoot(content, element.TryGetProperty("main", out var main) && main.GetBoolean());

        if (element.TryGetProperty("bounds", out var bounds) && bounds.ValueKind == JsonValueKind.Array
            && bounds.GetArrayLength() == 4)
        {
            root.Bounds = new Rect(
                bounds[0].GetDouble(), bounds[1].GetDouble(),
                bounds[2].GetDouble(), bounds[3].GetDouble());
        }

        if (element.TryGetProperty("bars", out var bars) && bars.ValueKind == JsonValueKind.Object)
        {
            foreach (var bar in bars.EnumerateObject())
            {
                if (!Enum.TryParse<DockZone>(bar.Name, out var edge) || !root.Bars.ContainsKey(edge)) continue;

                foreach (var group in bar.Value.EnumerateArray())
                {
                    var read = ReadGroup(group, known, ref well);
                    if (!read.IsEmpty) root.Bars[edge].Add(read);
                }
            }
        }

        root.DocumentWell = well;

        return content != null || HasBarredPanes(root) ? root : null;
    }

    private static PaneNode ReadNode(JsonElement element, DockingLayout layout, Func<string, bool> known, ref PaneNode well)
    {
        if (element.TryGetProperty("split", out var orientation))
        {
            var split = new PaneSplitNode
            {
                Orientation = Enum.TryParse<Orientation>(orientation.GetString(), out var value) ? value : Orientation.Horizontal,
                Length = ReadLength(element, "length")
            };

            if (element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    var node = ReadNode(child, layout, known, ref well);
                    if (node != null) split.Add(node);
                }
            }

            // A split that lost every child to a dropped document is not a split any more.
            if (split.Children.Count == 0) return null;

            if (element.TryGetProperty("well", out var isWell) && isWell.GetBoolean()) well = split;
            return split;
        }

        var group = ReadGroup(element, known, ref well);
        return group.IsEmpty ? null : group;
    }

    private static PaneGroupNode ReadGroup(JsonElement element, Func<string, bool> known, ref PaneNode well)
    {
        var group = new PaneGroupNode
        {
            Length = ReadLength(element, "length"),
            RestoreLength = ReadLength(element, "restore")
        };

        var dropped = 0;
        if (element.TryGetProperty("panes", out var panes) && panes.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            var saved = element.TryGetProperty("active", out var savedActive) && savedActive.TryGetInt32(out var a) ? a : 0;

            foreach (var pane in panes.EnumerateArray())
            {
                var id = pane.GetString();
                if (string.IsNullOrEmpty(id)) continue;

                if (known != null && !known(id))
                {
                    if (index < saved) dropped++;   // the active tab shifts down by whatever vanished ahead of it
                    index++;
                    continue;
                }

                group.PaneIds.Add(id);
                index++;
            }
        }

        group.ActiveIndex = element.TryGetProperty("active", out var active) && active.TryGetInt32(out var wasActive)
            ? group.IsEmpty ? -1 : Math.Clamp(wasActive - dropped, 0, group.PaneIds.Count - 1)
            : group.IsEmpty ? -1 : 0;

        if (element.TryGetProperty("state", out var state)
            && Enum.TryParse<PaneGroupState>(state.GetString(), out var parsed))
        {
            group.State = parsed;
        }

        // A REVEALED panel is not a state to come back in: it is a glance at a tool, and a layout that reopens with a
        // flyout hanging over it restores a gesture rather than an arrangement.
        if (group.State == PaneGroupState.Revealed) group.State = PaneGroupState.Collapsed;

        // An EMPTY area still counts: the centre is a place, and a layout saved with nothing open in it comes back with
        // the place, not without one.
        if (element.TryGetProperty("well", out var isWell) && isWell.GetBoolean()) well = group;

        return group;
    }

    private static PaneLength ReadLength(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? PaneLength.Parse(value.GetString())
            : PaneLength.Star;
    }

    private static bool Keep(string paneId, Func<string, bool> keep) => keep == null || keep(paneId);

    private static bool HasKeptPane(PaneNode node, Func<string, bool> keep)
    {
        switch (node)
        {
            case PaneGroupNode group:
                foreach (var id in group.PaneIds)
                {
                    if (Keep(id, keep)) return true;
                }
                return false;

            case PaneSplitNode split:
                foreach (var child in split.Children)
                {
                    if (HasKeptPane(child, keep)) return true;
                }
                return false;

            default:
                return false;
        }
    }

    // Which tab is active among the ones that survive the filter: dropping the documents ahead of it would otherwise
    // leave the index pointing past the end, or at somebody else.
    private static int ActiveAmongKept(PaneGroupNode group, Func<string, bool> keep)
    {
        var active = -1;
        var written = 0;

        for (var i = 0; i < group.PaneIds.Count; i++)
        {
            if (!Keep(group.PaneIds[i], keep)) continue;

            if (i == group.ActiveIndex) active = written;
            written++;
        }

        return active >= 0 ? active : written > 0 ? 0 : -1;
    }

    private static bool HasBarredPanes(DockingRoot root)
    {
        foreach (var bar in root.Bars.Values)
        {
            if (bar.Count > 0) return true;
        }

        return false;
    }
}
