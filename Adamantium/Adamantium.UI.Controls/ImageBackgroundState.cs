namespace Adamantium.UI.Controls;

/// <summary>When an <see cref="Image"/> paints its <see cref="Image.Background"/>.</summary>
public enum ImageBackgroundState
{
    /// <summary>Only while there is no picture to show - none named, or one still loading. An Image with no source
    /// draws NOTHING, which is a hole the size of its slot and reads as a broken layout rather than as a picture on its
    /// way; a ground gives it something to be seen and taken hold of, and gets out of the way the moment the picture
    /// arrives.</summary>
    WhenEmpty,

    /// <summary>Under the picture as well. A picture fitted by <c>Uniform</c> leaves a margin on two sides that has to
    /// be some colour, and one drawn with transparency has something to sit on.</summary>
    Always,

    /// <summary>Never - the element draws the picture and nothing else, whatever its background says.</summary>
    Never
}
