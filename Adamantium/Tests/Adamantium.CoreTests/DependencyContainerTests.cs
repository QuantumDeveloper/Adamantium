using Adamantium.Core.DependencyInjection;
using Adamantium.CoreTests.Services;
using NUnit.Framework;

namespace Adamantium.CoreTests
{
    public class DependencyContainerTests
    {
        private IDependencyResolver container;
        
        [SetUp]
        public void Setup()
        {
            container = new AdamantiumDependencyContainer();
            var containerRegistry = (IContainerRegistry)container;
            containerRegistry.Register<IContactsService, ContactsService>();
            containerRegistry.Register<ICommonService, CommonService>();
            containerRegistry.Register<IItemsApi, ItemsApi>();
            containerRegistry.Register<IItemsService, ItemsService>();
            containerRegistry.Register<IMediaService, MediaService>();
        }

        [Test]
        public void TransientTest()
        {
            var instance1 = container.Resolve<ICommonService>();
            var instance2 = container.Resolve<ICommonService>();
            Assert.IsFalse(instance1 == instance2);
            Assert.Pass();
        }
        
        [Test]
        public void SingletonTest()
        {
            container = new AdamantiumDependencyContainer();
            var containerRegistry = (IContainerRegistry)container;
            containerRegistry.Register<IContactsService, ContactsService>();
            containerRegistry.RegisterSingleton<ICommonService, CommonService>();
            containerRegistry.Register<IItemsApi, ItemsApi>();
            containerRegistry.Register<IItemsService, ItemsService>();
            containerRegistry.Register<IMediaService, MediaService>();
            
            var instance1 = container.Resolve<ICommonService>();
            var instance2 = container.Resolve<ICommonService>();
            Assert.IsTrue(instance1 == instance2);
            Assert.IsTrue(instance1.ContactsService == instance2.ContactsService);
            Assert.IsTrue(instance1.ItemsService == instance2.ItemsService);
            Assert.IsTrue(instance1.MediaService == instance2.MediaService);
            Assert.Pass();
        }
        
        [Test]
        public void SingletonItemsApiTest()
        {
            container = new AdamantiumDependencyContainer();
            var containerRegistry = (IContainerRegistry)container;
            containerRegistry.Register<IContactsService, ContactsService>();
            containerRegistry.Register<ICommonService, CommonService>();
            containerRegistry.RegisterSingleton<IItemsApi, ItemsApi>();
            containerRegistry.Register<IItemsService, ItemsService>();
            containerRegistry.Register<IMediaService, MediaService>();
            
            var instance1 = container.Resolve<ICommonService>();
            var instance2 = container.Resolve<ICommonService>();
            Assert.IsTrue(instance1.ItemsService.ItemsApi == instance2.ItemsService.ItemsApi);
            Assert.Pass();
        }

        [Test]
        public void NamedInstancesTest()
        {
            container = new AdamantiumDependencyContainer();
            var containerRegistry = (IContainerRegistry)container;
            containerRegistry.RegisterInstance<INamingService>(new NamingService("ContactsService1"), "ContactsService1");
            containerRegistry.RegisterInstance<INamingService>(new NamingService("ContactsService2"), "ContactsService2");

            var named1 = container.Resolve<INamingService>("ContactsService1");
            var named2 = container.Resolve<INamingService>("ContactsService2");
            
            Assert.IsTrue(named1.Name != named2.Name);
        }
        
        [Test]
        public void UnnamedInstancesTest()
        {
            container = new AdamantiumDependencyContainer();
            var containerRegistry = (IContainerRegistry)container;
            containerRegistry.RegisterInstance<INamingService>(new NamingService(""));
            containerRegistry.RegisterInstance<INamingService>(new NamingService("ContactsService2"), "ContactsService2");

            var named1 = container.Resolve<INamingService>();
            var named2 = container.Resolve<INamingService>("ContactsService2");
            
            Assert.IsTrue(string.IsNullOrEmpty(named1.Name));
            Assert.IsTrue(named2.Name == "ContactsService2");
        }
    }
}