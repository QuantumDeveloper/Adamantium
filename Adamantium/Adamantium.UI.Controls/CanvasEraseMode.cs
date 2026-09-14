namespace Adamantium.UI.Controls;

/// <summary>What the eraser takes. The same pair an ink surface has always offered, because they answer different
/// questions: one is "this line is wrong", the other is "this bit of it is".</summary>
public enum CanvasEraseMode
{
    /// <summary>Rubs a hole where the eraser went. A stroke crossed in the middle becomes two.</summary>
    Point,

    /// <summary>Takes the whole thing the eraser touched, however little of it was touched.</summary>
    Stroke
}
