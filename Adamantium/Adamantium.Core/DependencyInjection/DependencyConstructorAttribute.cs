using System;

namespace Adamantium.Core.DependencyInjection
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public class DependencyConstructorAttribute : Attribute
    {
    }
}