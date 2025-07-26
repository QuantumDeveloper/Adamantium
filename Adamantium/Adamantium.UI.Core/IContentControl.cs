namespace Adamantium.UI.Core;

public interface IContentControl : IControl, IContainer
{
    [Content]
    object Content { get; set; }
}