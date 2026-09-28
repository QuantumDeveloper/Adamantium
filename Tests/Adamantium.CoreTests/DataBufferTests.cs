using Adamantium.Core;
using NUnit.Framework;

namespace Adamantium.CoreTests;

[TestFixture]
public class DataBufferTests
{
    [Test]
    public void Clear_WritesTheValueOverEveryByte()
    {
        using var buffer = new DataBuffer(32);

        buffer.Clear(0xAB);
        Assert.That(buffer.GetRange<byte>(0, 32), Is.All.EqualTo((byte)0xAB));

        buffer.Clear();
        Assert.That(buffer.GetRange<byte>(0, 32), Is.All.EqualTo((byte)0));
    }
}
