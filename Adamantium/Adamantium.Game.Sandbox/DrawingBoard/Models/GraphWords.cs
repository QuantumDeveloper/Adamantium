namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>EVERY WORD THIS PAGE'S GRAPH IS BUILT OUT OF, in one place.
/// <para>None of them is decoration. What a socket CARRIES is what decides whether a wire may go on it and what colour
/// it wears; what a NODE is, is the word the catalogue is looked up with and the word a saved file names it by; what a
/// socket is CALLED is the address a saved wire finds its end at. Every one of them is matched against the same word
/// written somewhere else - so one typed twice is a wire that silently refuses to go on, a node that comes back blank
/// from a file, or a pin painted the theme's grey for no visible reason.</para>
/// <para>Grouped rather than laid out flat because the same word means different things in different places: "Color" is
/// a kind of node, AND a thing that flows, AND the name of a socket. Three constants called Color would be three ways
/// to reach for the wrong one.</para></summary>
public static class GraphWords
{
    /// <summary>WHAT FLOWS through a socket. Two sockets join when these agree, and the pin takes its colour from
    /// this - see <see cref="GraphSocketKind"/>, which must offer every one of them.</summary>
    public static class Flows
    {
        public const string Color = "Color";

        public const string Number = "Number";

        public const string Vector = "Vector";

        public const string Texture = "Texture";

        public const string Bool = "Bool";
    }

    /// <summary>WHAT A NODE IS. The word the palette, the drop-down and the loader all look the catalogue up with.
    /// </summary>
    public static class Kinds
    {
        public const string Color = "Color";

        public const string Number = "Number";

        public const string Mix = "Mix";

        public const string Add = "Add";

        public const string Scale = "Scale";

        public const string Gray = "Gray";

        public const string Merge = "Merge";

        public const string Output = "Output";
    }

    /// <summary>WHAT A SOCKET IS CALLED. A saved wire is written down as the two names it joins, so these are addresses
    /// as well as labels.</summary>
    public static class Sockets
    {
        public const string Out = "Out";

        public const string A = "A";

        public const string B = "B";

        public const string Amount = "Amount";

        public const string By = "By";

        public const string Color = "Color";

        public const string Colors = "Colors";
    }

    /// <summary>WHICH SIDE of a node - what the plus on an inspector section hands over, and the prefix a socket made
    /// there is named with. This word crosses from markup into code, which is the one seam where a typo cannot be
    /// caught by a compiler at all: mistyped, the plus on the outputs quietly adds an input.</summary>
    public static class Sides
    {
        public const string In = "In";

        public const string Out = "Out";
    }

    /// <summary>WHICH SECTION of the palette a kind stands in.</summary>
    public static class Groups
    {
        public const string Source = "Source";

        public const string Blend = "Blend";

        public const string Result = "Result";
    }
}
