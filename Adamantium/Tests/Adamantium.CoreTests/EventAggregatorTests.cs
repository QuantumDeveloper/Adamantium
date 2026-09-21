using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.CoreTests.Events;
using Adamantium.CoreTests.Services;
using NUnit.Framework;

namespace Adamantium.CoreTests
{
    public class EventAggregatorTests
    {
        private IEventAggregator eventAggregator;
        private IDependencyResolver container;
        private TestPayload payload;
        
        
        [SetUp]
        public void Setup()
        {
            container = new AdamantiumDependencyContainer();
            eventAggregator = container.Resolve<IEventAggregator>();
            payload = new TestPayload();
        }

        [Test]
        public void SubscribePublishTest()
        {
            eventAggregator.GetEvent<TestEvent>().Subscribe(TestAction);
            eventAggregator.GetEvent<TestEvent>().Publish();
        }
        
        [Test]
        public void UnsubscribeTest()
        {
            eventAggregator.GetEvent<TestEvent>().Subscribe(TestAction);
            eventAggregator.GetEvent<TestEvent>().Unsubscribe(TestAction);
            var contains = eventAggregator.GetEvent<TestEvent>().Contains(TestAction);
            Assert.IsFalse(contains);
        }
        
        [Test]
        public void SubscribePublishTestWithFilter()
        {
            eventAggregator.GetEvent<TestPayloadEvent>().Subscribe(TestPayloadAction);
            eventAggregator.GetEvent<TestPayloadEvent>().Publish(payload);
        }

        private void TestAction()
        {
            Assert.Pass();
        }

        private void TestPayloadAction(TestPayload testPayload)
        {
            Assert.IsTrue(testPayload.Id == payload.Id);
            Assert.Pass();
        }


    }
}