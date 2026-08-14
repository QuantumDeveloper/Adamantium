using System.Linq;
using Adamantium.UI.Core.Markup;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The runtime loader (the live designer and VisualRenderer run on it) has to read markup the same way the build does -
/// it shares the transformer with codegen, so what it must not do is answer DIFFERENTLY for the same markup.
/// </summary>
[TestFixture]
public class AumlRuntimeLoaderDirectiveTests
{
    // A View is an entity root, so the transformer walks it - the same walk codegen gets.
    private const string ViewRoot =
        "<View x:Namespace=\"Test.App\" xmlns=\"http://adamantium/ui\" xmlns:x=\"http://adamantium/ui/xaml/extensions\" ";

    // A directive nobody listed must fail the preview exactly as it fails the build, or markup that previews fine
    // breaks on build.
    [Test]
    public void AnUnknownDirectiveIsReportedByThePreviewToo()
    {
        var result = AumlLoader.Load(ViewRoot + "x:Nmae=\"Root\"><Grid /></View>");

        Assert.That(result.Diagnostics.Any(d => d.Contains("Unknown directive 'x:Nmae'")), Is.True,
            string.Join(" | ", result.Diagnostics));
    }

    [Test]
    public void AValueDirectiveOnTheElementIsReportedByThePreviewToo()
    {
        var result = AumlLoader.Load(ViewRoot + "x:Null=\"\"><Grid /></View>");

        Assert.That(result.Diagnostics.Any(d => d.Contains("belongs in a value")), Is.True,
            string.Join(" | ", result.Diagnostics));
    }

    // A FRAGMENT root (no Window/View/Page/Theme/StyleSet) is what the designer previews mid-edit, and it used to be
    // walked by nobody: the transformer stopped at a root it had no class to generate for, so nothing inside was
    // type-resolved or judged. The preview was therefore quieter than the build - this same typo passed here and failed
    // on compile.
    [Test]
    public void AFragmentRootIsJudgedLikeAnyOther()
    {
        var result = AumlLoader.Load(
            "<StackPanel xmlns=\"http://adamantium/ui\" xmlns:x=\"http://adamantium/ui/xaml/extensions\" " +
            "x:Nmae=\"Root\"><Grid /></StackPanel>");

        Assert.That(result.Diagnostics.Any(d => d.Contains("Unknown directive 'x:Nmae'")), Is.True,
            string.Join(" | ", result.Diagnostics));
    }

    // ...and it still BUILDS: judging it must not stop the preview from producing a tree.
    [Test]
    public void AFragmentRootStillPreviews()
    {
        var result = AumlLoader.Load(
            "<StackPanel xmlns=\"http://adamantium/ui\" xmlns:x=\"http://adamantium/ui/xaml/extensions\">" +
            "<Border /><Grid /></StackPanel>");

        Assert.That(result.Root, Is.InstanceOf<Adamantium.UI.Controls.Panels.StackPanel>(),
            string.Join(" | ", result.Diagnostics));
        Assert.That(((Adamantium.UI.Controls.Panels.StackPanel)result.Root).Children.Count, Is.EqualTo(2),
            "the children have to come through the walk, not be dropped by it");
    }
}
