using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Templates.GeometricPrimitives;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine
{
    public static class GeometricPrimitivesGenerator
    {
        /// <summary>
        /// Create primitive using default parameters
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="entityWorld"></param>
        /// <param name="primitiveType"></param>
        /// <param name="geometryType"></param>
        /// <param name="owner"></param>
        /// <param name="initialPosition"></param>
        /// <returns></returns>
        public static async Task<Entity> CreatePrimitive(EntityWorld entityWorld, Camera camera,
            ShapeType primitiveType, GeometryType geometryType, Entity owner = null, Vector3D? initialPosition = null)
        {
            switch (primitiveType)
            {
                case ShapeType.Plane:
                    return await CreatePlane(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Cube:
                    return await CreateCube(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Cylinder:
                    return await CreateCylinder(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Torus:
                    return await CreateTorus(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Tube:
                    return await CreateTube(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Cone:
                    return await CreateCone(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Teapot:
                    return await CreateTeapot(entityWorld, camera, owner, initialPosition);
                case ShapeType.Capsule:
                    return await CreateCapsule(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.UVSphere:
                case ShapeType.GeoSphere:
                case ShapeType.CubeSphere:
                    int tess = 3;
                    switch (primitiveType)
                    {
                        case ShapeType.CubeSphere:
                        case ShapeType.UVSphere:
                            tess = 30;
                            break;
                    }

                    return await CreateSphere(entityWorld, camera, geometryType, (SphereType) primitiveType, owner,
                        initialPosition, 1, tess);
                case ShapeType.Polygon:
                    return await CreatePolygon(entityWorld, camera, geometryType, Vector2F.One, owner, initialPosition);
                case ShapeType.Rectangle:
                    return await CreateRectangle(entityWorld, camera, geometryType, owner, initialPosition);
                case ShapeType.Ellipse:
                    return await CreateEllipse(entityWorld, camera, geometryType, EllipseType.EdgeToEdge, Vector2F.One,
                        owner, initialPosition);
                case ShapeType.Arc:
                    return await CreateArc(entityWorld, camera, geometryType, Vector2F.One, owner, initialPosition);
                case ShapeType.Line:
                    return await CreateLine(entityWorld, camera, geometryType, new Vector3F(-0.5f, 0, 0),
                        new Vector3F(0.5f, 0, 0), owner, initialPosition);
            }

            return null;
        }

        private static Entity EntityPostCreation(EntityWorld entityWorld, Camera camera, Entity primitive, Vector3D? initialPosition = null)
        {
            if (initialPosition == null && camera != null)
            {
                var collider = primitive.GetComponent<Collider>();
                var size = Vector3F.Average(collider.Bounds.Size);
                var pos = primitive.GetPositionForNewObject(camera, size);
                primitive.Transform.SetPosition(pos);
            }
            else if (initialPosition != null && camera != null)
            {
                primitive.Transform.SetPosition(initialPosition.Value);
            }
            entityWorld.AddEntity(primitive);
            return primitive;
        }

        public static async Task<Entity> CreatePlane(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float width = 1,
            float length = 1,
            int tesselation = 1,
            Matrix4x4F? transform = null)
        {
            var template = new PlaneTemplate(geometryType, width, length, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Plane", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateCube(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float width = 1,
            float height = 1,
            float depth = 1,
            int tesselation = 1,
            Matrix4x4F? transform = null)
        {
            var template = new CubeTemplate(geometryType, width, height, depth, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Cube", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateTorus(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float diameter = 1,
            float thickness = 0.33333f,
            int tesselation = 3,
            Matrix4x4F? transform = null)
        {
            var template = new TorusTemplate(geometryType, diameter, thickness, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Torus", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateCylinder(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float diameter = 1,
            float height = 1,
            int tesselation = 32,
            Matrix4x4F? transform = null)
        {
            var template = new CylinderTemplate(geometryType, height, diameter, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Cylinder", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateTube(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float diameter = 1f,
            float height = 1f,
            float thickness = 0.01f,
            int tesselation = 32,
            Matrix4x4F? transform = null)
        {
            var template = new TubeTemplate(geometryType, diameter, height, thickness, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Tube", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateCone(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float height = 1f,
            float topDiameter = 0f,
            float bottomDiameter = 1f,
            int tesselation = 32,
            Matrix4x4F? transform = null)
        {
            var template = new ConeTemplate(geometryType, topDiameter, bottomDiameter, height, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Cone", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateTeapot(
            EntityWorld entityWorld,
            Camera camera,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float size = 1,
            int tesselation = 8,
            Matrix4x4F? transform = null)
        {
            var template = new TeapotTemplate(size, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Teapot", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateSphere(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            SphereType sphereType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float diameter = 1,
            int tesselation = 3,
            Matrix4x4F? transform = null)
        {
            string name;
            switch (sphereType)
            {
                case SphereType.GeoSphere:
                    name = "GeoSphere";
                    break;
                case SphereType.CubeSphere:
                    name = "CubeSphere";
                    break;
                default:
                    name = "UVSphere";
                    break;
            }

            var template = new SphereTemplate(geometryType, sphereType, diameter, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, name, false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateCapsule(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float height = 1f,
            float radius = 1f,
            int tesselation = 40,
            Matrix4x4F? transform = null)
        {
            var template = new CapsuleTemplate(geometryType, radius, height, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Capsule", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreatePolygon(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Vector2F diameter,
            Entity owner = null,
            Vector3D? initialPosition = null,
            int tesselation = 40,
            Matrix4x4F? transform = null)
        {
            var template = new PolygonTemplate(geometryType, diameter, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Polygon", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateRectangle(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float width = 2,
            float height = 1,
            float radiusX = 0,
            float radiusY = 0,
            int tesselation = 40,
            Matrix4x4F? transform = null)
        {
            var template = new RectangleTemplate(geometryType, width, height, radiusX, radiusY, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Rectangle", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateEllipse(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            EllipseType ellipseType,
            Vector2F diameter,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float startAngle = 0,
            float stopAngle = 360,
            bool isClockwise = true,
            int tesselation = 40,
            Matrix4x4F? transform = null)
        {
            var template = new EllipseTemplate(geometryType, ellipseType, diameter, startAngle, stopAngle, isClockwise, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Ellipse", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateArc(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Vector2F diameter,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float thickness = 0.1f,
            float startAngle = 0,
            float stopAngle = 360,
            bool isClockwise = true,
            int tesselation = 40,
            Matrix4x4F? transform = null)
        {
            var template = new ArcTemplate(geometryType, diameter, thickness, startAngle, stopAngle, isClockwise, tesselation, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Arc", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }

        public static async Task<Entity> CreateLine(
            EntityWorld entityWorld,
            Camera camera,
            GeometryType geometryType,
            Vector3F startPoint,
            Vector3F endPoint,
            Entity owner = null,
            Vector3D? initialPosition = null,
            float thickness = 0.1f,
            Matrix4x4F? transform = null)
        {
            var template = new LineTemplate(geometryType, startPoint, endPoint, thickness, transform);
            var primitive = await entityWorld.CreateEntityFromTemplate(template, owner, "Line", false);
            return EntityPostCreation(entityWorld, camera, primitive, initialPosition);
        }
    }
}
