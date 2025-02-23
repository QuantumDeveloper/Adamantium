using System;
using System.ComponentModel;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics.Core;

public interface ITexture
{
    uint Width { get; }
    uint Height { get; }
    SurfaceFormat SurfaceFormat { get; }
    ImageLayout ImageLayout { get; set; }
    ulong TotalSizeInBytes { get; }
    unsafe IntPtr ManagedPointer { get; }
    unsafe void* NativePointer { get; }

    /// <summary>
    /// Gets the name of this component.
    /// </summary>
    /// <value>The name.</value>
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the tag associated to this object.
    /// </summary>
    /// <value>The tag.</value>
    object Tag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the name of this instance is immutable.
    /// </summary>
    /// <value><c>true</c> if this instance is name immutable; otherwise, <c>false</c>.</value>
    bool IsNameImmutable { get; set; }

    bool HasName { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is disposed.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
    /// </value>
    bool IsDisposed { get; }

    /// <summary>
    /// <see cref="GraphicsDevice"/>
    /// </summary>
    IGraphicsDevice GraphicsDevice { get; }
    
    Image GetImage();
    
    ImageView GetImageView();

    unsafe void Save(string path, ImageFileType fileType);

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources
    /// </summary>
    void Dispose();

    /// <summary>
    /// Raised when a public property of this object is set.
    /// </summary>
    event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Occurs when Dispose is called.
    /// </summary>
    event EventHandler<EventArgs> Disposing;
}