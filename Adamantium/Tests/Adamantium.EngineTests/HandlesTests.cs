using System;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Engine.Tools;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.EngineTests;

[TestFixture]
public class HandlesTests
{
    private static readonly Vector3 At = new(0, 0, 10);

    [Test]
    public void Move_AlongAnAxis_FollowsThePointer()
    {
        var scene = new ToolScene();
        var target = ToolScene.Target(At);
        var handles = new MoveHandles();

        Drag(scene, handles, target, "RightAxis", At + new Vector3(0.5, 0, 0), At + new Vector3(2.5, 0, 0));

        AssertNear(target.Transform.Position, At + new Vector3(2, 0, 0));
    }

    [Test]
    public void Move_AcrossAPlane_FollowsThePointer()
    {
        var scene = new ToolScene();
        var target = ToolScene.Target(At);
        var handles = new MoveHandles();

        Drag(scene, handles, target, "RightUpManipulator", At + new Vector3(0.5, 0.5, 0), At + new Vector3(1.5, -1, 0));

        AssertNear(target.Transform.Position, At + new Vector3(1, -1.5, 0));
    }

    [Test]
    public void Move_ArmAtTheEye_AndSquaresEdgeOn_AreHidden()
    {
        var scene = new ToolScene();
        var handles = new MoveHandles();

        handles.Place(ToolScene.Target(At), scene.Camera);

        Assert.That(IsShown(handles, "ForwardAxis", scene), Is.False);
        Assert.That(IsShown(handles, "ForwardAxisManipulator", scene), Is.False);
        Assert.That(IsShown(handles, "RightForwardManipulator", scene), Is.False);
        Assert.That(IsShown(handles, "UpForwardManipulator", scene), Is.False);
        Assert.That(IsShown(handles, "RightAxis", scene), Is.True);
        Assert.That(IsShown(handles, "UpAxisManipulator", scene), Is.True);
        Assert.That(IsShown(handles, "RightUpManipulator", scene), Is.True);
    }

    [Test]
    public void Move_Squares_SitOnTheSideTheCameraLooksFrom()
    {
        var scene = new ToolScene();
        var at = new Vector3(4, 4, 10);
        var handles = new MoveHandles();

        handles.Place(ToolScene.Target(at), scene.Camera);

        Assert.That(SideOf(CenterOf(handles, "RightUpManipulator", scene) - at), Is.EqualTo(new[] { -1, -1, 0 }));
        Assert.That(SideOf(CenterOf(handles, "RightForwardManipulator", scene) - at), Is.EqualTo(new[] { -1, 0, -1 }));
        Assert.That(SideOf(CenterOf(handles, "UpForwardManipulator", scene) - at), Is.EqualTo(new[] { 0, -1, -1 }));
    }

    [Test]
    public void Rotate_Rings_AreLinesInTheirOwnPlanes()
    {
        var handles = new RotationHandles();

        AssertRing(handles, "RightAxisOrbit", p => p.X);
        AssertRing(handles, "UpAxisOrbit", p => p.Y);
        AssertRing(handles, "ForwardAxisOrbit", p => p.Z);
    }

    [Test]
    public void Rotate_AboutAnAxis_TurnsTheGrabbedPointToThePointer()
    {
        var scene = new ToolScene();
        var target = ToolScene.Target(At);
        var handles = new RotationHandles();

        Drag(scene, handles, target, "ForwardAxisOrbit", At + new Vector3(1, 0, 0), At + new Vector3(0, 1, 0));

        var turned = Vector3F.TransformNormal(Vector3F.UnitX, target.Transform.GetLocalMatrixF());
        AssertNear((Vector3)turned, new Vector3(0, 1, 0));
        AssertNear(target.Transform.Position, At);
    }

    [Test]
    public void Scale_AlongAnAxis_StretchesOnlyThatAxis()
    {
        var scene = new ToolScene();
        var target = ToolScene.Target(At);
        var handles = new ScaleHandles();

        Drag(scene, handles, target, "RightAxis", At + new Vector3(1, 0, 0), At + new Vector3(2, 0, 0));

        AssertNear((Vector3)target.Transform.ScaleFactor, new Vector3(2, 1, 1));
    }

    [Test]
    public void Pivot_MovesThePivot_AndLeavesTheEntityInPlace()
    {
        var scene = new ToolScene();
        var target = ToolScene.Target(At);
        target.Transform.Rotation = QuaternionF.RotationAxis(Vector3F.UnitY, MathHelper.DegreesToRadians(30));
        var before = Vector3F.TransformCoordinate(new Vector3F(1, 2, 3), target.Transform.GetWorldMatrixF());
        var handles = new PivotHandles();

        Drag(scene, handles, target, "MoveUp", At + new Vector3(0, 0.6, 0), At + new Vector3(0, 2.6, 0));

        AssertNear(target.Transform.Pivot, At + new Vector3(0, 2, 0));
        var after = Vector3F.TransformCoordinate(new Vector3F(1, 2, 3), target.Transform.GetWorldMatrixF());
        AssertNear((Vector3)after, (Vector3)before);
    }

    [Test]
    public void PointLight_Anchor_DragsTheRange()
    {
        var scene = new ToolScene();
        var light = ToolScene.Target(At);
        light.AddComponent(new Light(LightType.Point));
        var handles = new PointLightHandles();

        Drag(scene, handles, light, "AnchorPointRight", At + new Vector3(1, 0, 0), At + new Vector3(3, 0, 0));

        Assert.That(light.GetComponent<Light>().Range, Is.EqualTo(3f).Within(1e-2f));
    }

    [Test]
    public void SpotLight_End_DragsTheRange_AndTheRimDragsTheAngle()
    {
        var scene = new ToolScene();
        var entity = ToolScene.Target(At);
        entity.AddComponent(new Light(LightType.Spot));
        var light = entity.GetComponent<Light>();
        var axis = (Vector3)Vector3F.Normalize(light.Direction);
        var handles = new SpotLightHandles();

        Drag(scene, handles, entity, "AnchorPointCenter", At + axis, At + axis * 4);
        Assert.That(light.Range, Is.EqualTo(4f).Within(1e-2f));

        var baseCenter = At + axis * 4;
        Drag(scene, handles, entity, "AnchorPointRight", baseCenter + new Vector3(light.SpotRadius, 0, 0), baseCenter + new Vector3(4, 0, 0));
        Assert.That(light.OuterSpotAngle, Is.EqualTo(MathHelper.DegreesToRadians(45)).Within(1e-2f));
    }

    [Test]
    public void SpotLight_TurnedEntity_DragsAlongWhereItShines()
    {
        var scene = new ToolScene();
        var entity = ToolScene.Target(At);
        entity.Transform.Rotation = QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(180));
        entity.AddComponent(new Light(LightType.Spot));
        var light = entity.GetComponent<Light>();
        var axis = (Vector3)light.Direction;
        var handles = new SpotLightHandles();

        Drag(scene, handles, entity, "AnchorPointCenter", At + axis, At + axis * 5);

        Assert.That(Vector3F.Dot(light.Direction, Vector3F.Up), Is.GreaterThan(0.999f));
        Assert.That(light.Range, Is.EqualTo(5f).Within(1e-2f));
    }

    [Test]
    public void LightHandles_ApplyOnlyToTheirKindOfLight()
    {
        var point = ToolScene.Target(At);
        point.AddComponent(new Light(LightType.Point));
        var spot = ToolScene.Target(At);
        spot.AddComponent(new Light(LightType.Spot));

        Assert.That(new PointLightHandles().AppliesTo(point), Is.True);
        Assert.That(new PointLightHandles().AppliesTo(spot), Is.False);
        Assert.That(new SpotLightHandles().AppliesTo(spot), Is.True);
        Assert.That(new SpotLightHandles().AppliesTo(ToolScene.Target(At)), Is.False);
    }

    private static void Drag(ToolScene scene, Handles handles, Entity target, string part, Vector3 from, Vector3 to)
    {
        handles.Place(target, scene.Camera);
        handles.BeginDrag(target, handles.Shape.Get(part), scene.RayAt(from));
        handles.Drag(target, scene.RayAt(to));
    }

    private static bool IsShown(Handles handles, string part, ToolScene scene)
    {
        return handles.Shape.Get(part).Transform.GetMetadata(scene.Camera).Enabled;
    }

    private static Vector3 CenterOf(Handles handles, string part, ToolScene scene)
    {
        var entity = handles.Shape.Get(part);
        var world = entity.Transform.GetMetadata(scene.Camera).WorldMatrixF;
        var points = entity.GetComponent<MeshData>().Mesh.Points;
        var sum = Vector3.Zero;
        foreach (var point in points)
        {
            sum += (Vector3)Vector3F.TransformCoordinate((Vector3F)point, world);
        }

        return sum / points.Length + scene.Camera.WorldPosition;
    }

    private static int[] SideOf(Vector3 offset)
    {
        return [Side(offset.X), Side(offset.Y), Side(offset.Z)];
    }

    private static int Side(double value)
    {
        return Math.Abs(value) < 1e-3 ? 0 : Math.Sign(value);
    }

    private static void AssertRing(Handles handles, string part, Func<Vector3, double> across)
    {
        var mesh = handles.Shape.Get(part).GetComponent<MeshData>().Mesh;
        Assert.That(mesh.MeshTopology, Is.EqualTo(PrimitiveType.LineList), part);
        Assert.That(mesh.Points.Length, Is.GreaterThan(8), part);
        foreach (var point in mesh.Points)
        {
            Assert.That(Math.Abs(across(point)), Is.LessThan(1e-4), $"{part}: {point}");
            Assert.That(point.Length(), Is.EqualTo(mesh.Points[0].Length()).Within(1e-3), $"{part}: {point}");
        }
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
    {
        Assert.That((actual - expected).Length(), Is.LessThan(1e-2), $"{actual} instead of {expected}");
    }
}
