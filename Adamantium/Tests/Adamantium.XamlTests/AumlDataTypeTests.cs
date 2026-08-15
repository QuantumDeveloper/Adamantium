using System.Linq;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// <c>x:DataType</c> declares what a DataTemplate is written against, the way <c>x:ViewModel</c> declares it for a view.
/// Nothing is generated from it - tooling resolves <c>{Binding}</c> paths inside the template against it. What IS checked
/// at build time is that the TYPE resolves: a renamed model must not leave a template pointing at nothing.
/// <para>Deliberately NOT checked: the path members. A view model's bindable members are produced by the MVVM generator,
/// and Roslyn generators cannot see each other's output - so member checking here would fail every honest binding. The
/// type is a plain class and always visible; that is the difference.</para>
/// </summary>
[TestFixture]
public class AumlDataTypeTests
{
    private static string Template(string dataType) =>
        AumlCodegenHarness.WindowHeader + "><ItemsControl><ItemsControl.ItemTemplate>" +
        $"<DataTemplate x:DataType=\"{dataType}\"><TextBlock Text=\"x\"/></DataTemplate>" +
        "</ItemsControl.ItemTemplate></ItemsControl></Window>";

    [Test]
    public void ATypeThatResolvesIsAccepted()
    {
        AumlCodegenHarness.Generate(Template("TextBlock"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
    }

    [Test]
    public void TheMarkupExtensionFormIsAcceptedToo()
    {
        AumlCodegenHarness.Generate(Template("{x:Type TextBlock}"), out var errors);

        Assert.That(errors, Is.Empty, AumlCodegenHarness.Errors(errors));
    }

    [Test]
    public void ATypeThatDoesNotResolveFailsTheBuild()
    {
        AumlCodegenHarness.Generate(Template("NoSuchModel"), out var errors);

        Assert.That(errors.Select(e => e.GetMessage()), Has.Some.Contains("x:DataType"),
            string.Join(" | ", errors.Select(e => e.GetMessage())));
    }

    // The template still builds: declaring the type is metadata for tooling, not something the generator consumes.
    [Test]
    public void TheTemplateIsStillGenerated()
    {
        var code = AumlCodegenHarness.Generate(Template("TextBlock"), out _);

        Assert.That(code, Does.Contain("DataTemplate"), "the directive must not swallow the template it sits on");
    }
}
