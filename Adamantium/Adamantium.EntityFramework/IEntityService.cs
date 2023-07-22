using System;

namespace Adamantium.EntityFramework;

public interface IEntityService : IUpdateService, IRenderService
{
    UInt128 Uid { get; }
    
    EntityWorld EntityWorld { get; }
    
    bool IsUpdateService { get; }
    
    bool IsRenderingService { get; }

    void Initialize();
}