using System;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Engine.EntityServices;
using Adamantium.Engine.Templates.Tools;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

/// <summary>
/// Rings that turn a target about a world axis or about the view, and a ball inside them that turns it freely. The
/// target turns about its pivot.
/// </summary>
public class RotationHandles : Handles
{
    private const float Radius = 2;

    private readonly Entity rightOrbit;
    private readonly Entity upOrbit;
    private readonly Entity forwardOrbit;
    private readonly Entity viewOrbit;
    private readonly Entity viewCircle;
    private readonly Entity ball;
    private Vector3 anchor;
    private QuaternionF startRotation;
    private Vector3F axis;
    private Vector3F startVector;
    private Vector3F viewForward;
    private float ballRadius;
    private bool free;

    public RotationHandles()
        : base(new RotationToolTemplate(2, new Vector3F(2)).BuildEntity(null, nameof(RotationHandles)))
    {
        rightOrbit = Shape.Get("RightAxisOrbit");
        upOrbit = Shape.Get("UpAxisOrbit");
        forwardOrbit = Shape.Get("ForwardAxisOrbit");
        viewOrbit = Shape.Get("CurrentViewManipulator");
        viewCircle = Shape.Get("CurrentViewCircle");
        ball = Shape.Get("CentralManipulator");
    }

    public override void Place(Entity target, Camera camera)
    {
        var at = PivotInWorld(target);
        var scale = Pixels * UnitsPerPixel(camera, at) / Radius;
        PlaceShape(camera, at, QuaternionF.Identity, scale);

        var facing = Matrix4x4F.Scaling(scale)
                     * Matrix4x4F.RotationQuaternion(FacingCamera(camera))
                     * Matrix4x4F.Translation(InRender(at, camera));
        PlacePart(viewOrbit, facing, camera);
        PlacePart(viewCircle, facing, camera);
    }

    public override void BeginDrag(Entity target, Entity handle, in PickRay ray)
    {
        anchor = PivotInWorld(target);
        startRotation = target.Transform.Rotation;
        viewForward = Vector3F.Normalize((Vector3F)ray.Camera.Forward);
        var origin = InRender(anchor, ray.Camera);

        free = handle == ball;
        if (free)
        {
            ballRadius = Pixels * UnitsPerPixel(ray.Camera, anchor);
            startVector = OnBall(ray.Ray, origin);
            return;
        }

        axis = handle == rightOrbit ? Vector3F.UnitX
            : handle == upOrbit ? Vector3F.UnitY
            : handle == forwardOrbit ? Vector3F.UnitZ
            : viewForward;
        OnPlane(ray.Ray, origin, axis, out var hit);
        startVector = hit - origin;
    }

    public override void Drag(Entity target, in PickRay ray)
    {
        var origin = InRender(anchor, ray.Camera);
        Vector3F turnAxis;
        float angle;

        if (free)
        {
            var now = OnBall(ray.Ray, origin);
            turnAxis = Vector3F.Cross(startVector, now);
            if (turnAxis.LengthSquared() < 1e-12f)
            {
                return;
            }

            turnAxis = Vector3F.Normalize(turnAxis);
            angle = AngleAbout(startVector, now, turnAxis);
        }
        else
        {
            if (!OnPlane(ray.Ray, origin, axis, out var hit))
            {
                return;
            }

            turnAxis = axis;
            angle = AngleAbout(startVector, hit - origin, axis);
        }

        var inParent = Vector3F.Normalize(ToParent(target, turnAxis));
        target.Transform.Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(inParent, angle), startRotation);
    }

    public override void Draw(EditorOverlayProcessor overlay)
    {
        overlay.DrawInScene(viewCircle);
        overlay.DrawInScene(viewOrbit);
        DrawOrbit(overlay, rightOrbit, Vector3F.UnitX);
        DrawOrbit(overlay, upOrbit, Vector3F.UnitY);
        DrawOrbit(overlay, forwardOrbit, Vector3F.UnitZ);
    }

    public override void Highlight(Entity handle)
    {
        base.Highlight(handle == ball ? viewCircle : handle);
    }

    private Vector3F OnBall(in Ray ray, Vector3F center)
    {
        if (!OnPlane(ray, center, viewForward, out var hit))
        {
            return viewForward;
        }

        var across = hit - center;
        var length = across.Length();
        if (length >= ballRadius)
        {
            return across * (ballRadius / length);
        }

        return across - viewForward * (float)Math.Sqrt(ballRadius * ballRadius - length * length);
    }
}
