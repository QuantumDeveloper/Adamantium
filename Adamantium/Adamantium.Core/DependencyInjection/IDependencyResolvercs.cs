using System;

namespace Adamantium.Core.DependencyInjection
{
    public interface IDependencyResolver
    {
        public T Resolve<T>(string name = "");

        public object Resolve(Type type, string name = "");

    }
}