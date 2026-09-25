using System;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Engine.EntityServices;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools;

/// <summary>
/// A set of handles a tool drags: their shape, where they stand for a target, and what dragging one does to it. The
/// shape keeps its size on screen whatever the distance.
/// </summary>
public abstract class Handles
{
    private const float FacingEye = 0.97f;
    private const float SquareEdgeOn = 0.05f;

    protected Handles(Entity shape)
    {
        Shape = shape;
    }

    public Entity Shape { get; }

    /// <summary>How big the handles look, in pixels.</summary>
    public float Pixels { get; set; } = 110;

    /// <summary>Whether these handles work on <paramref name="target"/>.</summary>
    public virtual bool AppliesTo(Entity target)
    {
        return target != null;
    }

    /// <summary>Stands the handles at <paramref name="target"/> as one camera sees them.</summary>
    public abstract void Place(Entity target, Camera camera);

    /// <summary>Starts dragging <paramref name="handle"/>, one of the shape's parts.</summary>
    public abstract void BeginDrag(Entity target, Entity handle, in PickRay ray);

    public abstract void Drag(Entity target, in PickRay ray);

    /// <summary>Draws the handles into one output's frame.</summary>
    public virtual void Draw(EditorOverlayProcessor overlay)
    {
        overlay.DrawInScene(Shape);
    }

    /// <summary>Lights up what dragging <paramref name="handle"/> would move; null lights up nothing.</summary>
    public virtual void Highlight(Entity handle)
    {
        Shape.TraverseInDepth(part => part.IsSelected = false);
        handle?.TraverseInDepth(part => part.IsSelected = true);
    }

    protected static Vector3 PivotInWorld(Entity target)
    {
        var pivot = target.Transform.Pivot;
        if (target.Owner == null)
        {
            return pivot;
        }

        return (Vector3)Vector3F.TransformCoordinate((Vector3F)pivot, target.Owner.Transform.GetWorldMatrixF());
    }

    protected static QuaternionF RotationInWorld(Entity target, QuaternionF local)
    {
        if (target.Owner == null)
        {
            return local;
        }

        var parent = target.Owner.Transform.GetWorldMatrixF();
        var basis = Matrix4x4F.Identity;
        basis.Right = Vector3F.Normalize(parent.Right);
        basis.Up = Vector3F.Normalize(parent.Up);
        basis.Forward = Vector3F.Normalize(parent.Forward);
        return QuaternionF.RotationMatrix(Matrix4x4F.RotationQuaternion(local) * basis);
    }

    protected static Vector3F ToParent(Entity target, Vector3F worldVector)
    {
        if (target.Owner == null)
        {
            return worldVector;
        }

        var parent = target.Owner.Transform.GetWorldMatrixF();
        Matrix4x4F.Invert(ref parent, out var inverse);
        return Vector3F.TransformNormal(worldVector, inverse);
    }

    protected static float UnitsPerPixel(CameraBase camera, Vector3 worldPoint)
    {
        return ScreenSpace.UnitsPerPixel(camera, worldPoint);
    }

    protected static Vector3F InRender(Vector3 worldPoint, CameraBase camera)
    {
        return ScreenSpace.InRender(worldPoint, camera);
    }

    protected static Vector3F Axis(QuaternionF axes, Vector3F unit)
    {
        return Vector3F.Normalize(Vector3F.Transform(unit, axes));
    }

    protected static QuaternionF FacingCamera(CameraBase camera)
    {
        return ScreenSpace.Facing(camera);
    }

    protected static QuaternionF RotationBetween(Vector3F from, Vector3F to)
    {
        var cosine = Vector3F.Dot(from, to);
        if (cosine > 0.99999f)
        {
            return QuaternionF.Identity;
        }

        var axis = Vector3F.Cross(from, to);
        if (axis.LengthSquared() < 1e-10f)
        {
            axis = Math.Abs(from.X) < 0.9f ? Vector3F.Cross(from, Vector3F.UnitX) : Vector3F.Cross(from, Vector3F.UnitY);
        }

        return QuaternionF.RotationAxis(Vector3F.Normalize(axis), (float)Math.Acos(Math.Clamp(cosine, -1f, 1f)));
    }

    protected static void PlaceAt(Entity part, Vector3 worldPoint, float pixels, CameraBase camera)
    {
        var metadata = part.Transform.GetMetadata(camera);
        metadata.WorldMatrixF = Matrix4x4F.Scaling(pixels * UnitsPerPixel(camera, worldPoint))
                                * Matrix4x4F.Translation(InRender(worldPoint, camera));
        metadata.Enabled = true;
    }

    protected void PlaceShape(Camera camera, Vector3 worldPoint, QuaternionF axes, float scale)
    {
        var placement = Matrix4x4F.Scaling(scale)
                        * Matrix4x4F.RotationQuaternion(axes)
                        * Matrix4x4F.Translation(InRender(worldPoint, camera));
        Shape.TraverseInDepth(part => PlacePart(part, placement, camera));
    }

    protected static void PlacePart(Entity part, Matrix4x4F placement, CameraBase camera)
    {
        var metadata = part.Transform.GetMetadata(camera);
        metadata.WorldMatrixF = part.Transform.GetLocalMatrixF() * placement;
        metadata.Enabled = true;
    }

    protected static void HideArmsFacingEye(Entity right, Entity up, Entity forward, QuaternionF axes, Vector3 at, CameraBase camera)
    {
        var sight = Vector3F.Normalize(InRender(at, camera));
        HideIf(right, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitX), sight)) >= FacingEye, camera);
        HideIf(up, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitY), sight)) >= FacingEye, camera);
        HideIf(forward, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitZ), sight)) >= FacingEye, camera);
    }

    protected static void DrawOrbit(EditorOverlayProcessor overlay, Entity orbit, Vector3F axis)
    {
        var world = orbit.Transform.GetMetadata(overlay.Camera).WorldMatrixF;
        var sight = Vector3F.Normalize(world.TranslationVector);
        var normal = Vector3F.Normalize(Vector3F.TransformNormal(axis, world));
        overlay.DrawInScene(orbit, Math.Abs(Vector3F.Dot(normal, sight)) < FacingEye);
    }

    protected static void HideSquaresEdgeOn(Entity rightUp, Entity rightForward, Entity upForward, QuaternionF axes, Vector3 at, CameraBase camera)
    {
        var sight = Vector3F.Normalize(InRender(at, camera));
        HideIf(rightUp, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitZ), sight)) < SquareEdgeOn, camera);
        HideIf(rightForward, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitY), sight)) < SquareEdgeOn, camera);
        HideIf(upForward, Math.Abs(Vector3F.Dot(Axis(axes, Vector3F.UnitX), sight)) < SquareEdgeOn, camera);
    }

    protected static bool IsPartOf(Entity handle, Entity part)
    {
        for (var at = handle; at != null; at = at.Owner)
        {
            if (at == part)
            {
                return true;
            }
        }

        return false;
    }

    protected static bool AlongLine(in Ray ray, Vector3F origin, Vector3F direction, out float along)
    {
        var offset = ray.Position - origin;
        var b = Vector3F.Dot(ray.Direction, direction);
        var denominator = 1 - b * b;
        if (denominator < 1e-6f)
        {
            along = 0;
            return false;
        }

        along = (Vector3F.Dot(direction, offset) - b * Vector3F.Dot(ray.Direction, offset)) / denominator;
        return true;
    }

    protected static bool OnPlane(in Ray ray, Vector3F point, Vector3F normal, out Vector3F hit)
    {
        var facing = Vector3F.Dot(normal, ray.Direction);
        if (Math.Abs(facing) < 1e-6f)
        {
            hit = Vector3F.Zero;
            return false;
        }

        var distance = Vector3F.Dot(normal, point - ray.Position) / facing;
        hit = ray.Position + ray.Direction * distance;
        return distance >= 0;
    }

    protected static float AngleAbout(Vector3F from, Vector3F to, Vector3F axis)
    {
        return (float)Math.Atan2(Vector3F.Dot(axis, Vector3F.Cross(from, to)), Vector3F.Dot(from, to));
    }

    private static void HideIf(Entity part, bool hidden, CameraBase camera)
    {
        if (hidden)
        {
            part.TraverseInDepth(current => current.Transform.GetMetadata(camera).Enabled = false);
        }
    }
}
