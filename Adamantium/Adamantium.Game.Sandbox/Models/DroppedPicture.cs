using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.Models;

/// <summary>
/// One picture that landed in the app, as DATA: the bytes exactly as they arrived (PNG, JPEG, GIF, TGA - the payload
/// never promised which) plus whether it is still being turned into something showable.
/// <para>
/// No image type here on purpose. The tile appears the moment the drop happens - decoding a large picture takes long
/// enough to be felt, and doing it inline would freeze the list - so the view decodes off-thread and fills the picture
/// in afterwards; this object only carries the bytes and says whether that is still pending.
/// </para>
/// </summary>
[ViewModel]
public partial class DroppedPicture : AdamantiumViewModel
{
    public DroppedPicture(byte[] bytes)
    {
        Bytes = bytes;
    }

    /// <summary>The encoded picture, as it arrived.</summary>
    public byte[] Bytes { get; }

    /// <summary>True until the view has decoded <see cref="Bytes"/> - what the tile's busy indicator follows.</summary>
    [Bindable] private bool _isLoading = true;
}
