namespace Adamantium.UI.Controls;

public interface IContentControl : IControl, IContainer
{
    object Content { get; set; }
}