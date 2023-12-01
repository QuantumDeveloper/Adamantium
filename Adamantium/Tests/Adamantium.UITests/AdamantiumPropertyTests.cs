using Adamantium.UI;
using Adamantium.UI.Controls;
using NUnit.Framework;

namespace Adamantium.UITests;

public class AdamantiumPropertyTests
{
    [Test]
    public void FindPropertyTest()
    {
        Border b = new Border();
        var properties = AdamantiumPropertyMap.GetRegistered(b);
    }
}