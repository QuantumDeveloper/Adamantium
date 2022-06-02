using System.IO;
using Adamantium.UI.Markup;
using NUnit.Framework;

namespace Adamantium.XamlTests;

public class XamlParseTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void BasicXamlParse()
    {
        //var res = XamlParser.Parse("MainWindow.xml");
        var str = File.ReadAllText("MainWindow.xml");
        var res = AumlParser.Parse(str);
    }
}