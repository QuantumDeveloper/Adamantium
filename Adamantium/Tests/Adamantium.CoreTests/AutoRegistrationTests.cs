using System;
using Adamantium.Core.DependencyInjection;
using NUnit.Framework;

namespace Adamantium.CoreTests
{
    public class AutoRegistrationTests
    {
        // --- fixtures: services + a view-model chain (the convention base stands in for AdamantiumViewModel) ---

        public interface IRepo { }

        [Service]
        public class Repo : IRepo { }

        public interface IClock { }

        [Service(ServiceLifetime.Singleton)]
        public class SystemClock : IClock { }

        public abstract class TestViewModelBase { }

        public class ChildViewModel : TestViewModelBase
        {
            public ChildViewModel(IRepo repo, IClock clock)
            {
                Repo = repo;
                Clock = clock;
            }

            public IRepo Repo { get; }
            public IClock Clock { get; }
        }

        public class RootViewModel : TestViewModelBase
        {
            public RootViewModel(ChildViewModel child, IClock clock)
            {
                Child = child;
                Clock = clock;
            }

            public ChildViewModel Child { get; }
            public IClock Clock { get; }
        }

        private static AdamantiumDependencyContainer CreateAutoRegisteredContainer()
        {
            var container = new AdamantiumDependencyContainer();
            ((IContainerRegistry)container).AutoRegister(
                new[] { typeof(AutoRegistrationTests).Assembly },
                typeof(TestViewModelBase));
            return container;
        }

        [Test]
        public void ResolvesDeepViewModelChainAutomatically()
        {
            var container = CreateAutoRegisteredContainer();

            var root = container.Resolve<RootViewModel>();

            Assert.IsNotNull(root);
            Assert.IsNotNull(root.Child);
            Assert.IsNotNull(root.Child.Repo);
            Assert.IsNotNull(root.Child.Clock);
        }

        [Test]
        public void ServiceSingletonIsSharedAcrossInterfaceAndConcreteAndChain()
        {
            var container = CreateAutoRegisteredContainer();

            var viaInterface = container.Resolve<IClock>();
            var viaConcrete = container.Resolve<SystemClock>();
            var root = container.Resolve<RootViewModel>();

            Assert.AreSame(viaInterface, viaConcrete);
            Assert.AreSame(viaInterface, root.Clock);
            Assert.AreSame(viaInterface, root.Child.Clock);
        }

        [Test]
        public void ViewModelsAreTransientByDefault()
        {
            var container = CreateAutoRegisteredContainer();

            var a = container.Resolve<RootViewModel>();
            var b = container.Resolve<RootViewModel>();

            Assert.AreNotSame(a, b);
            Assert.AreNotSame(a.Child, b.Child);
        }

        [Test]
        public void TransientServiceResolvedByInterfaceIsNewEachTime()
        {
            var container = CreateAutoRegisteredContainer();

            Assert.AreNotSame(container.Resolve<IRepo>(), container.Resolve<IRepo>());
        }

        // --- fallback / strict ---

        public class Unregistered { }

        public class NeedsUnregistered
        {
            public NeedsUnregistered(Unregistered dependency)
            {
                Dependency = dependency;
            }

            public Unregistered Dependency { get; }
        }

        [Test]
        public void FallbackAutoRegistersUnregisteredConcreteDependencyAndWarns()
        {
            var container = new AdamantiumDependencyContainer();
            ((IContainerRegistry)container).Register(typeof(NeedsUnregistered), typeof(NeedsUnregistered));
            string warned = null;
            container.Log = message => warned = message;

            var instance = container.Resolve<NeedsUnregistered>();

            Assert.IsNotNull(instance.Dependency);
            Assert.IsNotNull(warned);
        }

        [Test]
        public void StrictResolutionThrowsOnUnregisteredDependency()
        {
            var container = new AdamantiumDependencyContainer();
            var registry = (IContainerRegistry)container;
            registry.Register(typeof(NeedsUnregistered), typeof(NeedsUnregistered));
            registry.StrictResolution = true;

            Assert.Throws<ArgumentException>(() => container.Resolve<NeedsUnregistered>());
        }

        // --- circular dependency ---

        public class CycleA
        {
            public CycleA(CycleB b) { }
        }

        public class CycleB
        {
            public CycleB(CycleA a) { }
        }

        [Test]
        public void CircularDependencyThrows()
        {
            var container = new AdamantiumDependencyContainer();
            var registry = (IContainerRegistry)container;
            registry.Register(typeof(CycleA), typeof(CycleA));
            registry.Register(typeof(CycleB), typeof(CycleB));

            Assert.Throws<ArgumentException>(() => container.Resolve<CycleA>());
        }
    }
}
