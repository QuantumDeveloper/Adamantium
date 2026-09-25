using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;

namespace Adamantium.ECS.Components;

/// <summary>
/// A capsule round an entity: a segment along <see cref="Direction"/> through the middle of its mesh, rounded at both
/// ends. Fitted to the mesh when added: along the mesh's longest side, as thick as the wider of the other two.
/// </summary>
public class CapsuleCollider : Collider
{
    private readonly Dictionary<CameraBase, BoundingCapsule> colliderData = new();
    private BoundingCapsule capsule;
    private CapsuleDirection direction = CapsuleDirection.Y;
    private float radius;
    private float height;

    public CapsuleDirection Direction
    {
        get => direction;
        set
        {
            if (SetProperty(ref direction, value))
            {
                Rebuild();
            }
        }
    }

    public float Radius
    {
        get => radius;
        set
        {
            if (SetProperty(ref radius, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>End to end, the rounded ends included; a height below the diameter counts as the diameter.</summary>
    public float Height
    {
        get => height;
        set
        {
            if (SetProperty(ref height, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>The capsule in its entity's own space.</summary>
    public BoundingCapsule Capsule => capsule;

    public override void CalculateFromMesh(Mesh mesh)
    {
        base.CalculateFromMesh(mesh);
        var size = (Vector3F)Bounds.Size;
        Direction = size.X >= size.Y && size.X >= size.Z ? CapsuleDirection.X
            : size.Y >= size.Z ? CapsuleDirection.Y
            : CapsuleDirection.Z;
        Fit(size);
    }

    public override void ClearData()
    {
        colliderData.Clear();
    }

    public override bool ContainsDataFor(CameraBase camera)
    {
        return colliderData.ContainsKey(camera);
    }

    public override Mesh GetVisualRepresentation()
    {
        var turn = direction == CapsuleDirection.X ? Matrix4x4.RotationZ(MathHelper.DegreesToRadians(-90))
            : direction == CapsuleDirection.Z ? Matrix4x4.RotationX(MathHelper.DegreesToRadians(90))
            : Matrix4x4.Identity;
        Geometry = Shapes.Capsule.GenerateGeometry(GeometryType.Outlined, capsule.Length, radius * 2, 40, turn * Matrix4x4.Translation((Vector3)capsule.Center));
        return Geometry;
    }

    public override void UpdateForCamera(CameraBase camera)
    {
        colliderData[camera] = capsule.Transform(Owner.Transform.GetMetadata(camera).WorldMatrixF);
    }

    public override ContainmentType IsInsideCameraFrustum(Camera camera)
    {
        var placed = colliderData.TryGetValue(camera, out var data) ? data : capsule;
        return camera.Frustum.Contains(ref placed);
    }

    public override void Transform(ref Vector3F scale, ref QuaternionF rotation, ref Vector3F translation)
    {
        capsule = capsule.Transform(Matrix4x4F.Scaling(scale) * Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(translation));
    }

    public override void Transform(ref float uniformScale, ref QuaternionF rotation, ref Vector3F translation)
    {
        capsule = capsule.Transform(Matrix4x4F.Scaling(uniformScale) * Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(translation));
    }

    /// <summary>
    /// Grows the capsule, along the same direction, to take in <paramref name="collider"/> of this entity or of one
    /// under it, as it stands here.
    /// </summary>
    public override void Merge(Collider collider)
    {
        OrientedBoundingBox part = collider.Bounds;
        List<Vector3F> corners = [.. capsule.GetBoundingBox().GetCorners(), .. part.Transform(PlacementOf(collider.Owner)).GetCorners()];
        var merged = OrientedBoundingBox.FromPoints(corners);
        Bounds = Bounds.FromBoundingBox(merged);
        Fit(merged.Size);
    }

    public override bool Intersects(ref Ray ray, out Vector3F point)
    {
        return capsule.Intersects(ref ray, out point);
    }

    public override bool IntersectsForCamera(Camera camera, ref Ray ray, out Vector3F point)
    {
        point = Vector3F.Zero;
        return colliderData.TryGetValue(camera, out var placed) && placed.Intersects(ref ray, out point);
    }

    public override IConvexShape GetWorldShape()
    {
        return capsule.Transform(Owner.Transform.GetWorldMatrixF());
    }

    private void Fit(Vector3F size)
    {
        var along = direction == CapsuleDirection.X ? size.X : direction == CapsuleDirection.Y ? size.Y : size.Z;
        var across = direction == CapsuleDirection.X ? Math.Max(size.Y, size.Z)
            : direction == CapsuleDirection.Y ? Math.Max(size.X, size.Z)
            : Math.Max(size.X, size.Y);
        Radius = across / 2;
        Height = Math.Max(along, across);
        Rebuild();
    }

    private void Rebuild()
    {
        var axis = direction == CapsuleDirection.X ? Vector3F.UnitX : direction == CapsuleDirection.Y ? Vector3F.UnitY : Vector3F.UnitZ;
        var half = Math.Max(height / 2 - radius, 0);
        var center = LocalCenter;
        capsule = new BoundingCapsule(center - axis * half, center + axis * half, radius);
    }
}
