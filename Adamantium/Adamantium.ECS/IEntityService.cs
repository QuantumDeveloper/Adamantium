using System;
using System.Collections.Generic;

namespace Adamantium.ECS;

public interface IEntityService : IUpdateService, IRenderService
{
    UInt128 Uid { get; }

    /// <summary>
    /// Place of this service in the frame, lower first. States a dependency on other services, which is why it is
    /// one value for the whole cycle rather than one per phase.
    /// </summary>
    int Priority { get; set; }
    
    EntityWorld EntityWorld { get; }
    
    bool IsUpdateService { get; }
    
    bool IsRenderingService { get; }
    
    EntityServiceType ServiceType { get; }

    void Initialize();
    
    IReadOnlyList<IEntityProcessor> Processors { get; }

    void AttachProcessor(IEntityProcessor processor);

    void DetachProcessor(IEntityProcessor processor);
}