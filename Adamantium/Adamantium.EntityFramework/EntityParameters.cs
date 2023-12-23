using Adamantium.EntityFramework.ComponentsBasics;

namespace Adamantium.EntityFramework;

public class EntityParameters
{
    public EntityParameters(CameraProjectionType projectionType)
    {
        CameraProjectionType = projectionType;
    }
    public CameraProjectionType CameraProjectionType { get; set; }
}