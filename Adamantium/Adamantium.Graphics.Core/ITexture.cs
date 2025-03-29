using System;
using System.ComponentModel;
using Adamantium.Core;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics.Core;

public interface ITexture : INamedObject
{
    uint Width { get; }
    uint Height { get; }
    uint Depth { get; }
    SurfaceFormat SurfaceFormat { get; }
    ImageLayout ImageLayout { get; set; }
    ulong TotalSizeInBytes { get; }
    unsafe IntPtr ManagedPointer { get; }
    unsafe void* NativePointer { get; }
    MSAALevel MSAALevel { get; }
    
    ImageAspectFlagBits ImageAspect { get; }

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
    
    void SetImageView(ImageView imageView);

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