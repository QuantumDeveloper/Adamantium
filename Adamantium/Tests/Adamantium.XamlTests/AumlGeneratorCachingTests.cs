using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// An edit that touches no markup must not make the generator re-read the markup. 182 .auml files, about 2 MB, are
/// parsed on every run that misses - which is where the minute-long builds come from (docs/TECH_DEBT.md, "Сборка").
///
/// This has to be asserted here rather than timed from a build: the cache lives in the GeneratorDriver, and every csc
/// invocation builds a fresh one, so a command-line build cannot show a hit even when the pipeline is perfect.
/// </summary>
[TestFixture]
public class AumlGeneratorCachingTests
{
    private const string Markup =
        AumlCodegenHarness.WindowHeader + "><Grid><TextBlock Text=\"hello\"/></Grid></Window>";

    [Test]
    public void EditThatTouchesNoMarkup_LeavesTheParseCached()
    {
        var (driver, compilation) = AumlCodegenHarness.TrackingDriver(Markup);

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        // An edit somewhere else in the project: a new type nothing in the markup names.
        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace Other { internal sealed class Unrelated { } }"));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(edited);

        var steps = driver.GetRunResult().Results[0].TrackedSteps;
        var parseSteps = steps
            .Where(step => step.Key.Contains(AumlParseStepName))
            .SelectMany(step => step.Value)
            .SelectMany(run => run.Outputs)
            .ToArray();

        Assert.That(parseSteps, Is.Not.Empty,
            "no parse step is tracked by that name - the pipeline was renamed, and this test is now blind. " +
            "Tracked steps: " + string.Join(", ", steps.Keys));

        Assert.That(
            parseSteps.All(o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
            "markup was re-parsed after an edit that touched no markup: " +
            string.Join(", ", parseSteps.Select(o => o.Reason)));
    }

    /// <summary>The name the parse step is registered under - see AumlCodeBehindGenerator.Initialize.</summary>
    private const string AumlParseStepName = "AumlParse";
}
