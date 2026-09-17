using System;
using System.Collections.Generic;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

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
