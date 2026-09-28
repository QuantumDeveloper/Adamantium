using System;

namespace Adamantium.CoreTests.Events
{
    public class TestPayload
    {
        public string Id;

        public TestPayload()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}