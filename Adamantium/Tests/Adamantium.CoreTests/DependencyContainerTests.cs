using Adamantium.Core.DependencyInjection;
using Adamantium.CoreTests.Services;
using NUnit.Framework;

namespace Adamantium.CoreTests
{
    public class DependencyContainerTests
    {
        private IDependencyContainer container;
        
        [SetUp]
        public void Setup()
        {
            container = new DependencyContainer();
            container.Register<IContactsService, ContactsService>();
            container.Register<ICommonService, CommonService>();
            container.Register<IItemsApi, ItemsApi>();
            container.Register<IItemsService, ItemsService>();
            container.Register<IMediaService, MediaService>();
        }

        [Test]
        public void ResolveInstanceFromDI()
        {
            var instance = container.Resolve<ICommonService>();
            Assert.Pass();
        }
    }
}