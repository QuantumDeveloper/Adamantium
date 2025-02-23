using System;

namespace Adamantium.ECS;

[Flags]
public enum EntityServiceType
{
    Update = 0,
    Render = 1,
    Submission = 2
}