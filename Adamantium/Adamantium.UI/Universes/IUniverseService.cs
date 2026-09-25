using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Game;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.UI.Universes;

public interface IUniverseService
{
    public IReadOnlyList<IUniverse> Universes { get; }

    public T CreateUniverse<T>(string name, IWindow wnd, EntityService service, params object[] args) where T : IUniverse;

    public bool RemoveUniverse(IUniverse universe);

    public void RunUniverses(IRenderService renderService, AppTime time);

    public void CopyOutput(IGraphicsDevice graphicsDevice);

    public event Action<IUniverse> OnUniverseAdded;
}
