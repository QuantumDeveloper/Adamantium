using System;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components
{
    public sealed class MeshData : ActivatableComponent
    {
        private Mesh mesh;
        private MeshMetadata metadata;

        public MeshData()
        {
            Metadata = MeshMetadata.Default();
        }

        public MeshData(MeshMetadata metadata)
        {
            Metadata = new MeshMetadata(metadata);
        }

        [DoNotClone]
        public Mesh Mesh
        {
            get => mesh;
            set
            {
                if (SetProperty(ref mesh, value))
                {
                    MeshDataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public MeshMetadata Metadata
        {
            get => metadata;
            set
            {
                if (SetProperty(ref metadata, value))
                {
                    MetadataChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler MeshDataChanged;

        public event EventHandler MetadataChanged;

        public void ApplyChanges()
        {
            if (Metadata.ShapeType == ShapeType.Unknown) return;
            
            Mesh shape = null;
            switch (Metadata.ShapeType)
            {
                case ShapeType.Plane:
                    shape = Shapes.Plane.GenerateGeometry(Metadata.GeometryType, Metadata.Width, Metadata.Depth, Metadata.TessellationFactor);
                    break;
                case ShapeType.Cube:
                    shape = Shapes.Cube.GenerateGeometry(Metadata.GeometryType, Metadata.Width, Metadata.Height, Metadata.Depth, Metadata.TessellationFactor);
                    break;
                case ShapeType.Cylinder:
                    shape = Shapes.Cylinder.GenerateGeometry(Metadata.GeometryType, Metadata.Height, Metadata.Diameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.Cone:
                    shape = Shapes.Cone.GenerateGeometry(Metadata.GeometryType, Metadata.Height, Metadata.TopDiameter, Metadata.BottomDiameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.Capsule:
                    shape = Shapes.Capsule.GenerateGeometry(Metadata.GeometryType, Metadata.Height, Metadata.Diameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.Torus:
                    shape = Shapes.Torus.GenerateGeometry(Metadata.GeometryType, Metadata.Diameter, Metadata.Thickness, Metadata.TessellationFactor);
                    break;
                case ShapeType.Tube:
                    shape = Shapes.Tube.GenerateGeometry(Metadata.GeometryType, Metadata.Diameter, Metadata.Height, Metadata.Thickness, Metadata.TessellationFactor);
                    break;
                case ShapeType.UVSphere:
                    shape = Shapes.Sphere.GenerateGeometry(Metadata.GeometryType, SphereType.UVSphere, Metadata.Diameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.GeoSphere:
                    shape = Shapes.Sphere.GenerateGeometry(Metadata.GeometryType, SphereType.GeoSphere, Metadata.Diameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.CubeSphere:
                    shape = Shapes.Sphere.GenerateGeometry(Metadata.GeometryType, SphereType.CubeSphere, Metadata.Diameter, Metadata.TessellationFactor);
                    break;
                case ShapeType.Polygon:
                    shape = Shapes.Polygon.GenerateGeometry(Metadata.GeometryType, new Vector2F(Metadata.Width, Metadata.Height), Metadata.TessellationFactor);
                    break;
                case ShapeType.Teapot:
                    shape = Shapes.Teapot.GenerateGeometry(Metadata.GeometryType, Metadata.Width, Metadata.TessellationFactor);
                    break;
                case ShapeType.Ellipse:
                    shape = Shapes.Ellipse.GenerateGeometry(Metadata.GeometryType, Metadata.EllipseType, new Vector2F(Metadata.Width, Metadata.Height), Metadata.StartAngle, Metadata.StopAngle, Metadata.IsClockwise, Metadata.TessellationFactor);
                    break;
                case ShapeType.Arc:
                    shape = Shapes.Arc.GenerateGeometry(Metadata.GeometryType, new Vector2F(Metadata.Width, Metadata.Height), Metadata.Thickness, Metadata.StartAngle, Metadata.StopAngle, Metadata.IsClockwise, Metadata.TessellationFactor);
                    break;
                case ShapeType.Rectangle:
                    shape = Shapes.Rectangle.GenerateGeometry(Metadata.GeometryType, Metadata.Width, Metadata.Height, Metadata.Corners, Metadata.TessellationFactor);
                    break;
                case ShapeType.Line:
                    shape = Shapes.Line.GenerateGeometry(Metadata.GeometryType, Metadata.LineStart, Metadata.LineEnd, Metadata.Thickness);
                    break;
            }

            if (shape != null)
            {
                Mesh = shape;
            }
        }

        public override IComponent Clone()
        {
            var meshComponent = new MeshData(Metadata);
            meshComponent.Mesh = Mesh.Clone();
            return meshComponent;
        }
    }
}
