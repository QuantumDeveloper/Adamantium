using System.Collections.Generic;
using Adamantium.Engine.Core;
using NUnit.Framework;

namespace Adamantium.UITests
{
    public class UidGeneratorTests
    {
        [Test]
        public void TestForUnique()
        {
            var ids = new HashSet<long>();
            var minimumAcceptibleIterations = 2000000;
            for (int i = 0; i < minimumAcceptibleIterations; i++)
            {
                var result = UidGenerator.Generate();
                Assert.IsTrue(!ids.Contains(result), $"Collision on run {i} with ID '{result}'");
                ids.Add(result);
            }
        }
    }
}