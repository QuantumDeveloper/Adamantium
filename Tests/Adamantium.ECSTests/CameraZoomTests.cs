using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.ECSTests;

[TestFixture]
public class CameraZoomTests
{
    [Test]
    public void AnOrbitingCamera_ClosesTheDistanceByAStepANotch_WhateverTheFrameRate()
    {
        var camera = NewCamera();
        camera.SetThirdPersonCamera(new Entity(null, "Subject"), Vector3F.Zero, CameraType.ThirdPersonFree, distanceToObject: 20);

        camera.Zoom(1);
        Assert.That(camera.Radius, Is.EqualTo(20 / camera.ZoomStep).Within(1e-9));

        camera.Zoom(-1);
        Assert.That(camera.Radius, Is.EqualTo(20).Within(1e-9));
    }

    [Test]
    public void AnOrbitingCamera_StopsShortOfItsSubject()
    {
        var camera = NewCamera();
        camera.SetThirdPersonCamera(new Entity(null, "Subject"), Vector3F.Zero, CameraType.ThirdPersonLocked, distanceToObject: 20);

        camera.Zoom(1000);

        Assert.That(camera.Radius, Is.EqualTo(camera.ZNear * 2).Within(1e-6));
    }

    [Test]
    public void AFreeCamera_MovesAlongItsView_ByItsSpeedANotch()
    {
        var camera = NewCamera();
        camera.Velocity = 3;
        camera.WheelVelocity = 1;
        var start = camera.Owner.Transform.Position;

        camera.Zoom(2);

        var moved = camera.Owner.Transform.Position - start;
        Assert.That(moved.Length(), Is.EqualTo(6).Within(1e-6));
        Assert.That(Vector3.Dot(Vector3.Normalize(moved), camera.Forward), Is.GreaterThan(0.9999));
    }

    private static Camera NewCamera()
    {
        var camera = new Camera(60, 800, 600, 0.1f, 10000f);
        new Entity(null, "Camera").AddComponent(camera);
        camera.Update(new AppTime());
        return camera;
    }
}
