using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components.Extensions
{
    public static class Collisions
    {
        public static Ray CalculateRay(
           Vector2F mousePosition,
           CameraBase camera,
           Matrix4x4F worldMatrix,
           bool reversedDepth = true)
        {
            Matrix4x4F worldviewProj = worldMatrix * camera.ViewMatrix * camera.ProjectionMatrix;

            //For reverse depth buffer
            if (reversedDepth)
            {
                return Ray.GetPickRay(
                   (int)mousePosition.X,
                   (int)mousePosition.Y,
                   new ViewportF(0, 0, camera.Width, camera.Height, 1, 0),
                   worldviewProj);
            }
            else
            {
                return Ray.GetPickRay(
                   (int)mousePosition.X,
                   (int)mousePosition.Y,
                   new ViewportF(0, 0, camera.Width, camera.Height, 0, 1),
                   worldviewProj);
            }
        }

        public static Ray CalculateRay(
           Vector2F cursorPosition,
           CameraBase camera,
           Matrix4x4F world,
           Matrix4x4F projection,
           bool useViewMatrix,
           bool reversedDepth = true)
        {
            Matrix4x4F wvp = Matrix4x4F.Identity;
            if (useViewMatrix)
            {
                wvp = world * camera.ViewMatrix * projection;
            }
            else
            {
                wvp = world * projection;
            }

            //For reverse depth buffer
            if (reversedDepth)
            {
                return Ray.GetPickRay(
                   (int)cursorPosition.X,
                   (int)cursorPosition.Y,
                   new ViewportF(0, 0, camera.Width, camera.Height, 1, 0),
                   wvp);
            }
            else
            {
                return Ray.GetPickRay(
                   (int)cursorPosition.X,
                   (int)cursorPosition.Y,
                   new ViewportF(0, 0, camera.Width, camera.Height, 0, 1),
                   wvp);
            }
        }

        public static CollisionResult Intersects(
           this Entity owner,
           CameraBase camera,
           Vector2F cursorPosition,
           bool useViewMatrix,
           Matrix4x4F projectionMatrix,
           CollisionMode collisionMode,
           CompareOrder compareOrder,
           float limitDistance,
           bool reverseDepth = true)
        {
            CollisionResult result = null;
            switch (collisionMode)
            {
                case CollisionMode.FirstAvailableCollider:
                    result = FindFirstAvailableCollider(
                        owner,
                        cursorPosition,
                        camera,
                        useViewMatrix,
                        projectionMatrix,
                        compareOrder,
                        reverseDepth);
                    break;
                case CollisionMode.IgnoreNonGeometryParts:
                    result = IgnoreNonGeometryParts(
                        owner,
                        cursorPosition,
                        camera,
                        useViewMatrix,
                        projectionMatrix,
                        compareOrder,
                        reverseDepth);
                    break;
                case CollisionMode.HighestPrecision:
                    result = HighestPrecision(
                        owner,
                        cursorPosition,
                        camera,
                        useViewMatrix,
                        projectionMatrix,
                        limitDistance,
                        compareOrder,
                        reverseDepth);
                    break;
                case CollisionMode.Mixed:
                case CollisionMode.MixedPreferCollider:
                    result = MixedCollisionDetection(
                        owner,
                        cursorPosition,
                        camera,
                        collisionMode,
                        useViewMatrix,
                        projectionMatrix,
                        limitDistance,
                        compareOrder,
                        reverseDepth);
                    break;
                case CollisionMode.CollidersOnly:
                    result = CollidersOnlyDetection(
                        owner,
                        cursorPosition,
                        camera,
                        collisionMode,
                        useViewMatrix,
                        projectionMatrix,
                        limitDistance,
                        compareOrder,
                        reverseDepth);
                    break;
            }

            return result;
        }

        public static CollisionResult
           Intersects(
              this Entity owner,
              CameraBase camera,
              Vector2F cursorPosition,
              CollisionMode collisionMode,
              CompareOrder compareOrder,
              float limitDistance)
        {
            return Intersects(
               owner,
               camera,
               cursorPosition,
               true,
               camera.ProjectionMatrix,
               collisionMode,
               compareOrder,
               limitDistance);
        }

        private static (bool Intersects, Vector3F Point) Intersects(
           this Entity owner,
           Ray ray,
           Matrix4x4F worldTransform,
           CollisionMode collisionMode,
           float limitDistance)
        {
            Vector3F interPoint = Vector3F.Zero;
            Vector3F point = Vector3F.Zero;
            bool intersects = false;

            switch (collisionMode)
            {
                case CollisionMode.FirstAvailableCollider:
                    intersects = IntersectsWithFirstAvailableCollider(owner, ref ray, out point);
                    break;
                case CollisionMode.IgnoreNonGeometryParts:
                    intersects = IntersectsIgnoreNonGeometryParts(owner, ref ray, out point);
                    break;
                case CollisionMode.HighestPrecision:
                    intersects = IntersectsHighestPrecision(owner, ref ray, limitDistance, out point);
                    break;
                case CollisionMode.Mixed:
                case CollisionMode.MixedPreferCollider:
                    intersects = IntersectsIgnoreNonGeometryParts(owner, ref ray, out point);
                    if (!intersects)
                    {
                        intersects = IntersectsHighestPrecision(owner, ref ray, limitDistance, out point);
                    }
                    break;
                case CollisionMode.CollidersOnly:
                    intersects = IntersectsCollidersOnly(owner, ref ray, out point);
                    break;
            }
            if (intersects)
            {
                interPoint = Vector3F.TransformCoordinate(point, worldTransform);
            }
            return (intersects, interPoint);
        }

        private static CollisionResult FindFirstAvailableCollider(
            Entity owner,
            Vector2F cursorPosition,
            CameraBase camera,
            bool useViewMatrix,
            Matrix4x4F projectionMatrix,
            CompareOrder compareOrder,
            bool reverseDepth = true)
        {
            CollisionResult collisionResult = new CollisionResult(compareOrder);

            var stack = new Stack<Entity>();
            stack.Push(owner);
            while (stack.Count > 0)
            {
                Entity current = stack.Pop();
                if (current.IsEnabled)
                {
                    var transformData = current.Transform.GetMetadata(camera);
                    if (!transformData.Enabled)
                    {
                        continue;
                    }
                    var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix, reverseDepth);
                    var result = Intersects(current, ray, transformData.WorldMatrix, CollisionMode.FirstAvailableCollider, 0);
                    if (result.Intersects)
                    {
                        collisionResult.ValidateAndSetValues(current, result.Point, true);
                        break;
                    }

                    for (int i = 0; i < current.Dependencies.Count; i++)
                    {
                        stack.Push(current.Dependencies[i]);
                    }
                }
            }
            return collisionResult;
        }

        private static CollisionResult IgnoreNonGeometryParts(
            Entity owner,
            Vector2F cursorPosition,
            CameraBase camera,
            bool useViewMatrix,
            Matrix4x4F projectionMatrix,
            CompareOrder compareOrder,
            bool reverseDepth = true)
        {
            CollisionResult collisionResult = new CollisionResult(compareOrder);
            owner.TraverseByLayer(
                current =>
                {
                    var transformData = current.Transform.GetMetadata(camera);
                    if (!transformData.Enabled)
                    {
                        return;
                    }
                    var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix, reverseDepth);
                    var result = Intersects(current, ray, transformData.WorldMatrix, CollisionMode.IgnoreNonGeometryParts, 0);
                    if (result.Intersects)
                    {
                        collisionResult.ValidateAndSetValues(current, result.Point, true);
                    }
                });
            return collisionResult;
        }

        private static CollisionResult HighestPrecision(
            Entity owner,
            Vector2F cursorPosition,
            CameraBase camera,
            bool useViewMatrix,
            Matrix4x4F projectionMatrix,
            float limitDistance,
            CompareOrder compareOrder,
            bool reverseDepth = true)
        {
            CollisionResult collisionResult = new CollisionResult(compareOrder);
            
            Stack<Entity> stack = new Stack<Entity>();
            stack.Push(owner);
            Boolean intersectionPresent = false;
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current.IsEnabled)
                {
                    var transformData = current.Transform.GetMetadata(camera);
                    if (!transformData.Enabled)
                    {
                        continue;
                    }
                    var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix, reverseDepth);
                    var result = Intersects(current, ray, transformData.WorldMatrix, CollisionMode.IgnoreNonGeometryParts, limitDistance);
                    if (result.Intersects)
                    {
                        intersectionPresent = true;
                        break;
                    }
                    foreach (var dependency in current.Dependencies)
                    {
                        stack.Push(dependency);
                    }
                }
            }
            if (intersectionPresent)
            {
                owner.TraverseByLayer(
                    current =>
                    {
                        var transformData = current.Transform.GetMetadata(camera);
                        if (!transformData.Enabled)
                        {
                            return;
                        }
                        var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix);
                        var result = Intersects(current, ray, transformData.WorldMatrix, CollisionMode.HighestPrecision, limitDistance);
                        if (result.Intersects)
                        {
                            collisionResult.ValidateAndSetValues(current, result.Point, true);
                        }
                    });
            }
            return collisionResult;
        }

        private static CollisionResult MixedCollisionDetection(
            Entity owner,
            Vector2F cursorPosition,
            CameraBase camera,
            CollisionMode collisionMode,
            bool useViewMatrix,
            Matrix4x4F projectionMatrix,
            float limitDistance,
            CompareOrder compareOrder,
            bool reverseDepth = true)
        {
            CollisionResult collisionResult = new CollisionResult(compareOrder);
            owner.TraverseByLayer(
                current =>
                {
                    if (!current.IgnoreInCollisionDetection)
                    {
                        var transformData = current.Transform.GetMetadata(camera);
                        if (!transformData.Enabled)
                        {
                            return;
                        }
                        var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix, reverseDepth);
                        var result = Intersects(current, ray, transformData.WorldMatrix, collisionMode, limitDistance);
                        if (result.Intersects)
                        {
                            collisionResult.ValidateAndSetValues(current, result.Point, true);
                            //var containsCollider = current.ContainsComponent<Collider>();
                            //if ((collisionMode == CollisionMode.Mixed && !containsCollider) ||
                            //    (collisionMode == CollisionMode.MixedPreferCollider && containsCollider))
                            //{
                            //    return;
                            //}
                        }
                    }
                });

            return collisionResult;
        }


        private static CollisionResult CollidersOnlyDetection(
            Entity owner,
            Vector2F cursorPosition,
            CameraBase camera,
            CollisionMode collisionMode,
            bool useViewMatrix,
            Matrix4x4F projectionMatrix,
            float limitDistance,
            CompareOrder compareOrder,
            bool reverseDepth = true)
        {
            CollisionResult collisionResult = new CollisionResult(compareOrder);
            owner.TraverseByLayer(
                current =>
                {
                    if (!current.IgnoreInCollisionDetection)
                    {
                        var transformData = current.Transform.GetMetadata(camera);
                        var containsCollider = current.ContainsComponent<Collider>();
                        if (!transformData.Enabled)
                        {
                            return;
                        }

                        if (containsCollider)
                        {
                            var ray = CalculateRay(cursorPosition, camera, transformData.WorldMatrix, projectionMatrix, useViewMatrix, reverseDepth);
                            var result = Intersects(current, ray, transformData.WorldMatrix, collisionMode, limitDistance);
                            if (result.Intersects)
                            {
                                collisionResult.ValidateAndSetValues(current, result.Point, true);
                            }
                        }
                    }
                });
            return collisionResult;
        }

        private static bool IntersectsWithFirstAvailableCollider(Entity owner, ref Ray ray, out Vector3F point)
        {
            bool intersects = false;
            point = Vector3F.Zero;
            var collision = owner.GetComponent<Collider>();
            if (collision != null)
            {
                intersects = collision.Intersects(ref ray, out point);
            }
            return intersects;
        }

        private static bool IntersectsIgnoreNonGeometryParts(Entity owner, ref Ray ray, out Vector3F point)
        {
            bool intersects = false;
            point = Vector3F.Zero;

            var meshData = owner.GetComponent<MeshData>();
            if (meshData == null)
            {
                point = Vector3F.Zero;
                return false;
            }

            var collision = owner.GetComponent<Collider>();
            if (collision != null)
            {
                intersects = collision.Intersects(ref ray, out point);
            }

            return intersects;
        }

        private static bool IntersectsCollidersOnly(Entity owner, ref Ray ray, out Vector3F point)
        {
            bool intersects = false;
            point = Vector3F.Zero;

            var collision = owner.GetComponent<Collider>();
            if (collision != null)
            {
                intersects = collision.Intersects(ref ray, out point);
            }

            return intersects;
        }

        private static bool IntersectsHighestPrecision(Entity owner, ref Ray ray, float limitDistance, out Vector3F point)
        {
            bool intersects = false;
            point = Vector3F.Zero;
            var meshData = owner.GetComponent<MeshData>();
            if (meshData == null)
            {
                point = Vector3F.Zero;
                return false;
            }

            if (meshData.Mesh.MeshTopology == PrimitiveType.LineStrip ||
                meshData.Mesh.MeshTopology == PrimitiveType.LineList)
            {
                if (meshData.Mesh.Indices.Length > 0)
                {
                    for (int i = 1; i < meshData.Mesh.Indices.Length; ++i)
                    {
                        if (meshData.Mesh.Indices[i - 1] != -1 && meshData.Mesh.Indices[i] != -1)
                        {
                            var position0 = meshData.Mesh.Positions[meshData.Mesh.Indices[i - 1]];
                            var position1 = meshData.Mesh.Positions[meshData.Mesh.Indices[i]];
                            intersects = ray.Intersects(ref position0, ref position1, limitDistance, out _, out point);
                            if (intersects)
                                break;
                        }
                    }
                }
                else
                {
                    int increment = meshData.Mesh.MeshTopology == PrimitiveType.LineStrip ? 1 : 2;
                    for (int i = 1; i < meshData.Mesh.Positions.Length; i += increment)
                    {
                        var position0 = meshData.Mesh.Positions[i - 1];
                        var position1 = meshData.Mesh.Positions[i];
                        intersects = ray.Intersects(ref position0, ref position1, limitDistance, out _, out point);
                        if (intersects)
                            break;
                    }
                }
            }
            else if (meshData.Mesh.MeshTopology == PrimitiveType.TriangleList ||
                     meshData.Mesh.MeshTopology == PrimitiveType.TriangleStrip)
            {
                var geometry = owner.GetComponent<MeshData>().Mesh;
                var indices = geometry.Indices;
                int increment = meshData.Mesh.MeshTopology == PrimitiveType.TriangleStrip ? 1 : 3;
                if (indices.Length >= 3)
                {
                    for (int i = 3; i < indices.Length; i += increment)
                    {
                        var vertex1 = geometry.Positions[indices[i - 2]];
                        var vertex2 = geometry.Positions[indices[i - 1]];
                        var vertex3 = geometry.Positions[indices[i]];
                        intersects = ray.Intersects(ref vertex1, ref vertex2, ref vertex3, out point);
                        if (intersects)
                            break;
                    }
                }
                else
                {
                    for (int i = 2; i < geometry.Positions.Length; i += increment)
                    {
                        var vertex1 = geometry.Positions[indices[i - 2]];
                        var vertex2 = geometry.Positions[indices[i - 1]];
                        var vertex3 = geometry.Positions[indices[i]];
                        intersects = ray.Intersects(ref vertex1, ref vertex2, ref vertex3, out point);
                        if (intersects)
                            break;
                    }
                }
            }

            return intersects;
        }
    }
}
