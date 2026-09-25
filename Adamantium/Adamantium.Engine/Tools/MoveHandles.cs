using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Engine.Templates.Tools;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

/// <summary>
/// Arrows that move a target along an axis, squares that move it across a plane, and a center that moves it across the
/// view. The world's axes. The squares sit by the center, on the side the camera looks from.
/// </summary>
public class MoveHandles : Handles
{
    private const float Length = 2;

    private readonly Entity right;
    private readonly Entity up;
    private readonly Entity forward;
    private readonly Entity rightUp;
    private readonly Entity rightForward;
    private readonly Entity upForward;
    private Vector3 anchor;
    private Vector3 startPosition;
    private Vector3F direction;
    private bool alongAxis;
    private float startAlong;
    private Vector3F startOffset;

    public MoveHandles()
        : base(new MoveToolTemplate(1, new Vector3F(2)).BuildEntity(null, nameof(MoveHandles)))
    {
        right = Shape.Get("RightAxis");
        up = Shape.Get("UpAxis");
        forward = Shape.Get("ForwardAxis");
        rightUp = Shape.Get("RightUpManipulator");
        rightForward = Shape.Get("RightForwardManipulator");
        upForward = Shape.Get("UpForwardManipulator");
    }

    public override void Place(Entity target, Camera camera)
    {
        var at = PivotInWorld(target);
        PlaceShape(camera, at, QuaternionF.Identity, Pixels * UnitsPerPoint(camera, at) / Length);
        HideArmsFacingEye(right, up, forward, QuaternionF.Identity, at, camera);
        HideSquaresEdgeOn(rightUp, rightForward, upForward, QuaternionF.Identity, at, camera);
        TurnSquaresToEye(QuaternionF.Identity, at, camera);
    }

    public override void BeginDrag(Entity target, Entity handle, in PickRay ray)
    {
        anchor = PivotInWorld(target);
        startPosition = target.Transform.Position;
        var origin = InRender(anchor, ray.Camera);

        alongAxis = IsPartOf(handle, right) || IsPartOf(handle, up) || IsPartOf(handle, forward);
        if (alongAxis)
        {
            direction = IsPartOf(handle, right) ? Vector3F.UnitX : IsPartOf(handle, up) ? Vector3F.UnitY : Vector3F.UnitZ;
            AlongLine(ray.Ray, origin, direction, out startAlong);
            return;
        }

        direction = handle == rightUp ? Vector3F.UnitZ
            : handle == rightForward ? Vector3F.UnitY
            : handle == upForward ? Vector3F.UnitX
            : Vector3F.Normalize((Vector3F)ray.Camera.Forward);
        OnPlane(ray.Ray, origin, direction, out var hit);
        startOffset = hit - origin;
    }

    public override void Drag(Entity target, in PickRay ray)
    {
        var origin = InRender(anchor, ray.Camera);
        Vector3F moved;
        if (alongAxis)
        {
            if (!AlongLine(ray.Ray, origin, direction, out var along))
            {
                return;
            }

            moved = direction * (along - startAlong);
        }
        else
        {
            if (!OnPlane(ray.Ray, origin, direction, out var hit))
            {
                return;
            }

            moved = hit - origin - startOffset;
        }

        target.Transform.Position = startPosition + (Vector3)ToParent(target, moved);
    }

    public override void Highlight(Entity handle)
    {
        base.Highlight(AxisOf(handle) ?? handle);
    }

    private static void Mirror(Entity part, Vector3F signs, Camera camera)
    {
        var metadata = part.Transform.GetMetadata(camera);
        metadata.WorldMatrixF = Matrix4x4F.Scaling(signs) * metadata.WorldMatrixF;
    }

    private void TurnSquaresToEye(QuaternionF axes, Vector3 at, Camera camera)
    {
        var toEye = -InRender(at, camera);
        var x = Vector3F.Dot(Axis(axes, Vector3F.UnitX), toEye) < 0 ? -1f : 1f;
        var y = Vector3F.Dot(Axis(axes, Vector3F.UnitY), toEye) < 0 ? -1f : 1f;
        var z = Vector3F.Dot(Axis(axes, Vector3F.UnitZ), toEye) < 0 ? -1f : 1f;
        Mirror(rightUp, new Vector3F(x, y, 1), camera);
        Mirror(rightForward, new Vector3F(x, 1, z), camera);
        Mirror(upForward, new Vector3F(1, y, z), camera);
    }

    private Entity AxisOf(Entity handle)
    {
        if (IsPartOf(handle, right))
        {
            return right;
        }

        if (IsPartOf(handle, up))
        {
            return up;
        }

        return IsPartOf(handle, forward) ? forward : null;
    }
}
