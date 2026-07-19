using Adamantium.ECS.Components;

namespace Adamantium.ECS;

public class EntityParameters
{
    public EntityParameters(CameraProjectionType projectionType)
    {
        CameraProjectionType = projectionType;
    }
    public CameraProjectionType CameraProjectionType { get; set; }
}