using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Navigation;
using NUnit.Framework;

namespace Adamantium.UITests
{
    /// <summary>One view-model read through SEVERAL views. A long page split into readable files is still one object with
    /// one set of bindings; what changes between the pieces is only which markup is shown.
    /// <para>Before this, a region tracked view-model TYPES alone, so two views of one model were indistinguishable to
    /// it - and the only way to switch between them was an empty wrapper type per view, which is a workaround for the
    /// mechanism rather than the mechanism.</para></summary>
    [TestFixture]
    public class SharedViewModelViewsTests
    {
        private sealed class SharedViewModel { }

        // Stand-ins for the markup files. The locator maps TYPES, so what they contain is beside the point here.
        private sealed class DefaultFace { }
        private sealed class FirstFace { }
        private sealed class SecondFace { }

        private sealed class OneResolver : IDependencyResolver
        {
            private readonly object _instance;
            public OneResolver(object instance) => _instance = instance;
            public T Resolve<T>(string name = "") => (T)_instance;
            public object Resolve(Type type, string name = "") => _instance;
        }

        // The locator answers per KEY, and still answers the plain question for anything that never names one.
        [Test]
        public void TheLocatorKeepsAViewPerKey()
        {
            var locator = new ViewLocator();
            locator.Register(typeof(SharedViewModel), typeof(DefaultFace));
            locator.RegisterView(typeof(SharedViewModel), "first", typeof(FirstFace));
            locator.RegisterView(typeof(SharedViewModel), "second", typeof(SecondFace));

            Assert.That(locator.ResolveViewType(typeof(SharedViewModel), "first"), Is.EqualTo(typeof(FirstFace)));
            Assert.That(locator.ResolveViewType(typeof(SharedViewModel), "second"), Is.EqualTo(typeof(SecondFace)));
            Assert.That(locator.ResolveViewType(typeof(SharedViewModel)), Is.EqualTo(typeof(DefaultFace)));
        }

        /// <summary>No key means the default view. A key that names nothing means NOTHING - not the default.
        /// <para>Falling back there looks harmless until the default view is the one HOSTING the region: the region then
        /// puts a second copy of that whole screen inside itself, recursively, and the screen simply appears twice with
        /// nothing to say why. Seen live on the brushes tab, on the stands that had no view registered yet.</para></summary>
        [Test]
        public void AnUnknownKeyResolvesToNothingRatherThanTheDefaultView()
        {
            var locator = new ViewLocator();
            locator.Register(typeof(SharedViewModel), typeof(DefaultFace));
            locator.RegisterView(typeof(SharedViewModel), "first", typeof(FirstFace));

            Assert.That(locator.ResolveViewType(typeof(SharedViewModel), "nobody-registered-this"), Is.Null);
            Assert.That(locator.ResolveViewType(typeof(SharedViewModel), null), Is.EqualTo(typeof(DefaultFace)));
            Assert.That(locator.ResolveViewType(typeof(SharedViewModel), string.Empty), Is.EqualTo(typeof(DefaultFace)));
        }

        /// <summary>The heart of it: both navigations land on the SAME view-model instance, so the only thing that moves
        /// is the key - and it has to move, or an adapter comparing view-models sees nothing happen.</summary>
        [Test]
        public void NavigatingBetweenTwoViewsOfOneModelMovesTheKey()
        {
            var shared = new SharedViewModel();
            var region = new Adamantium.Navigation.Region("stands", new OneResolver(shared), null);

            region.NavigateToViewAsync<SharedViewModel>("first").GetAwaiter().GetResult();
            Assert.That(region.CurrentViewModel, Is.SameAs(shared));
            Assert.That(region.CurrentViewKey, Is.EqualTo("first"));

            region.NavigateToViewAsync<SharedViewModel>("second").GetAwaiter().GetResult();
            Assert.That(region.CurrentViewModel, Is.SameAs(shared), "the model is shared - it must not be rebuilt per view");
            Assert.That(region.CurrentViewKey, Is.EqualTo("second"));
        }

        // Back and forward have to restore the FACE as well as the model, which is why the journal entry carries the key.
        [Test]
        public void GoingBackRestoresTheViewItLeftFrom()
        {
            var shared = new SharedViewModel();
            var region = new Adamantium.Navigation.Region("stands", new OneResolver(shared), null);

            region.NavigateToViewAsync<SharedViewModel>("first").GetAwaiter().GetResult();
            region.NavigateToViewAsync<SharedViewModel>("second").GetAwaiter().GetResult();

            region.GoBackAsync().GetAwaiter().GetResult();
            Assert.That(region.CurrentViewKey, Is.EqualTo("first"));

            region.GoForwardAsync().GetAwaiter().GetResult();
            Assert.That(region.CurrentViewKey, Is.EqualTo("second"));
        }

        /// <summary>An object that OWNS its region shows itself through it, and the very first of those navigations runs
        /// while its own constructor is still going. Resolving the type then either recurses or hands back a second copy,
        /// so the instance has to be given - and the container must not be consulted at all.</summary>
        [Test]
        public void NavigatingToAGivenInstanceNeverAsksTheContainer()
        {
            var shared = new SharedViewModel();
            var resolver = new ThrowingResolver();
            var region = new Adamantium.Navigation.Region("stands", resolver, null);

            region.NavigateToInstanceAsync(shared, "first").GetAwaiter().GetResult();

            Assert.That(region.CurrentViewModel, Is.SameAs(shared));
            Assert.That(region.CurrentViewKey, Is.EqualTo("first"));
            Assert.That(resolver.Asked, Is.False, "the caller handed over the instance - resolving one is the bug this guards");
        }

        private sealed class ThrowingResolver : IDependencyResolver
        {
            public bool Asked { get; private set; }

            public T Resolve<T>(string name = "")
            {
                Asked = true;
                throw new InvalidOperationException("the container must not be asked");
            }

            public object Resolve(Type type, string name = "")
            {
                Asked = true;
                throw new InvalidOperationException("the container must not be asked");
            }
        }

        // A navigation that names no key must leave the key empty, so every region that never heard of this keeps
        // behaving exactly as it did.
        [Test]
        public void AKeylessNavigationLeavesNoKey()
        {
            var shared = new SharedViewModel();
            var region = new Adamantium.Navigation.Region("stands", new OneResolver(shared), null);

            region.NavigateToAsync<SharedViewModel>().GetAwaiter().GetResult();

            Assert.That(region.CurrentViewModel, Is.SameAs(shared));
            Assert.That(region.CurrentViewKey, Is.Null);
        }
    }
}
