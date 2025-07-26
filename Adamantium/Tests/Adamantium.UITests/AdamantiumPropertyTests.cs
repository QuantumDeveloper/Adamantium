using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

public class AdamantiumPropertyTests
{
    [Test]
    public void FindPropertyTest()
    {
        var b = new Border();
        var properties = AdamantiumPropertyMap.GetRegistered(b);
    }
}