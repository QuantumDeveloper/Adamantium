using System;
using System.Collections.Generic;

namespace Adamantium.ECS;

public interface IEntityService : IUpdateService, IRenderService
{
    UInt128 Uid { get; }
    
    EntityWorld EntityWorld { get; }
    
    bool IsUpdateService { get; }
    
    bool IsRenderingService { get; }
    
    EntityServiceType ServiceType { get; }

    void Initialize();
    
    IReadOnlyList<IEntityProcessor> Processors { get; }

    void AttachProcessor(IEntityProcessor processor);

    void DetachProcessor(IEntityProcessor processor);
}