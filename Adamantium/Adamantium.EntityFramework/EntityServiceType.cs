using System;

namespace Adamantium.EntityFramework;

[Flags]
public enum EntityServiceType
{
    Update = 0,
    Render = 1,
    Submission = 2
}