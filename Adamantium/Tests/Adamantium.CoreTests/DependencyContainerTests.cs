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
            container = new AdamantiumServiceLocator();
            container.Register<IContactsService, ContactsService>();
            container.Register<ICommonService, CommonService>();
            container.Register<IItemsApi, ItemsApi>();
            container.Register<IItemsService, ItemsService>();
            container.Register<IMediaService, MediaService>();
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
            container = new AdamantiumServiceLocator();
            container.Register<IContactsService, ContactsService>();
            container.RegisterSingleton<ICommonService, CommonService>();
            container.Register<IItemsApi, ItemsApi>();
            container.Register<IItemsService, ItemsService>();
            container.Register<IMediaService, MediaService>();
            
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
            container = new AdamantiumServiceLocator();
            container.Register<IContactsService, ContactsService>();
            container.Register<ICommonService, CommonService>();
            container.RegisterSingleton<IItemsApi, ItemsApi>();
            container.Register<IItemsService, ItemsService>();
            container.Register<IMediaService, MediaService>();
            
            var instance1 = container.Resolve<ICommonService>();
            var instance2 = container.Resolve<ICommonService>();
            Assert.IsTrue(instance1.ItemsService.ItemsApi == instance2.ItemsService.ItemsApi);
            Assert.Pass();
        }

        [Test]
        public void NamedInstancesTest()
        {
            container = new AdamantiumServiceLocator();
            container.RegisterInstance<INamingService>(new NamingService("ContactsService1"), "ContactsService1");
            container.RegisterInstance<INamingService>(new NamingService("ContactsService2"), "ContactsService2");

            var named1 = container.Resolve<INamingService>("ContactsService1");
            var named2 = container.Resolve<INamingService>("ContactsService2");
            
            Assert.IsTrue(named1.Name != named2.Name);
        }
        
        [Test]
        public void UnnamedInstancesTest()
        {
            container = new AdamantiumServiceLocator();
            container.RegisterInstance<INamingService>(new NamingService(""));
            container.RegisterInstance<INamingService>(new NamingService("ContactsService2"), "ContactsService2");

            var named1 = container.Resolve<INamingService>();
            var named2 = container.Resolve<INamingService>("ContactsService2");
            
            Assert.IsTrue(string.IsNullOrEmpty(named1.Name));
            Assert.IsTrue(named2.Name == "ContactsService2");
        }
    }
}