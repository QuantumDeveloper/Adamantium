using System.IO;
using Adamantium.UI.Markup;
using Adamantium.UI.Templates;
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
    
    [Test]
    public void XamlControlTemplateParse()
    {
        //var res = XamlParser.Parse("MainWindow.xml");
        var str = File.ReadAllText("XamlTest1.xml");
        var res = AumlParser.Parse(str);
        ControlTemplate.Load(res);
    }
    
    [Test]
    public void XamlControlTemplateParseAndLoad()
    {
        //var res = XamlParser.Parse("MainWindow.xml");
        var str = File.ReadAllText("ControlTemplateTest2.xml");
        var res = AumlParser.Parse(str, true);
        var ct = ControlTemplate.Load(res);
        ct.Build();
    }
    
    [Test]
    public void AumlThemeParsing()
    {
        var res = AumlParser.Load("Theme.xml");
    }
}