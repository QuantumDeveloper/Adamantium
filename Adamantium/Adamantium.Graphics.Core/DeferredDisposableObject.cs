using System;
using Adamantium.Core;

namespace Adamantium.Graphics.Core;

public class DeferredDisposableObject : DisposableObject
{
    private IGraphicsDevice _graphicsDevice;
    
    public DeferredDisposableObject(IGraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void DeferDispose()
    {
        _graphicsDevice.AddToDeferDisposeQueue(this);
    }
}