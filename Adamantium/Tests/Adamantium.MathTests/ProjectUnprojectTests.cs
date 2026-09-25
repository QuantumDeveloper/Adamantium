using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests;

[TestFixture]
public class ProjectUnprojectTests
{
    private static readonly Matrix4x4F Projection = Matrix4x4F.PerspectiveFovX(60, 4f / 3, 0.1f, 1000);

    [Test]
    public void Single_UnprojectUndoesProject()
    {
        var point = new Vector3F(1.5f, -2, 10);

        var pixel = Vector3F.Project(point, 0, 0, 800, 600, 0, 1, Projection);
        var back = Vector3F.Unproject(pixel, 0, 0, 800, 600, 0, 1, Projection);

        Assert.That((back - point).Length(), Is.LessThan(1e-3f), $"{back} instead of {point}");
    }

    [Test]
    public void Double_UnprojectUndoesProject()
    {
        var projection = (Matrix4x4)Projection;
        var point = new Vector3(1.5, -2, 10);

        var pixel = Vector3.Project(point, 0, 0, 800, 600, 0, 1, projection);
        var back = Vector3.Unproject(pixel, 0, 0, 800, 600, 0, 1, projection);

        Assert.That((back - point).Length(), Is.LessThan(1e-3), $"{back} instead of {point}");
    }
}
