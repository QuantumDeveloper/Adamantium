using System.IO;
using Adamantium.UI.Xaml;
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
        var xamlString = File.ReadAllText("XamlTest1.xml");
        var res = XamlParser.Parse(xamlString);
    }
}