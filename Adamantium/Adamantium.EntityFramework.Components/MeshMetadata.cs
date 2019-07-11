using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components
{
    public class MeshMetadata : PropertyChangedBase
    {
        private ShapeType _shapeType;
        private int _tessellationFactor;
        private float _width;
        private float _height;
        private float _depth;
        private float _thickness;
        private float _diameter;
        private float _topDiameter;
        private float _bottomDiameter;
        private GeometryType _geometryType;
        private float _radiusX;
        private float _radiusY;
        private float _startAngle;
        private float _stopAngle;
        private bool _isClockwise;
        private EllipseType _ellipseType;
        private Vector3D _lineStart;
        private Vector3D _lineEnd;

        public MeshMetadata()
        { }

        public MeshMetadata(
            MeshMetadata metadata)
        {
            _shapeType = metadata._shapeType;
            _tessellationFactor = metadata.TessellationFactor;
            _width = metadata.Width;
            _height = metadata.Height;
            _depth = metadata.Depth;
            _thickness = metadata.Thickness;
            _diameter = metadata.Diameter;
            _topDiameter = metadata.TopDiameter;
            _bottomDiameter = metadata.BottomDiameter;
            _lineStart = metadata.LineStart;
            _lineEnd = metadata.LineEnd;
        }

        public static MeshMetadata Default()
        {
            var metadata = new MeshMetadata();
            metadata.ShapeType = ShapeType.Unknown;
            metadata.Width = 1;
            metadata.Height = 1;
            metadata.Depth = 1;
            metadata.Diameter = 1;
            metadata.TessellationFactor = GetDefaultTessFactor(metadata.ShapeType);
            metadata.Thickness = 0.1f;
            metadata.TopDiameter = 0;
            metadata.BottomDiameter = 1;
            metadata.LineStart = new Vector3D(-0.5f, 0, 0);
            metadata.LineStart = new Vector3D(0.5f, 0, 0);
            return metadata;
        }

        private static int GetDefaultTessFactor(ShapeType shapeType)
        {
            switch (shapeType)
            {
                case ShapeType.UVSphere:
                case ShapeType.CubeSphere:
                case ShapeType.Cone:
                case ShapeType.Tube:
                case ShapeType.Cylinder:
                case ShapeType.Torus:
                case ShapeType.Arc:
                case ShapeType.Ellipse:
                case ShapeType.Capsule:
                case ShapeType.Polygon:
                    return 40;
                case ShapeType.GeoSphere:
                case ShapeType.Teapot:
                    return 8;
                case ShapeType.Rectangle:
                    return 20;
                default:
                    return 1;
            }
        }

        public ShapeType ShapeType
        {
            get => _shapeType;
            set => SetProperty(ref _shapeType, value);
        }

        public GeometryType GeometryType
        {
            get => _geometryType;
            set => SetProperty(ref _geometryType, value);
        }

        public int TessellationFactor
        {
            get => _tessellationFactor;
            set => SetProperty(ref _tessellationFactor, value);
        }

        public float Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public float Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public float RadiusX
        {
            get => _radiusX;
            set => SetProperty(ref _radiusX, value);
        }

        public float RadiusY
        {
            get => _radiusY;
            set => SetProperty(ref _radiusY, value);
        }

        public float StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value);
        }

        public float StopAngle
        {
            get => _stopAngle;
            set => SetProperty(ref _stopAngle, value);
        }

        public EllipseType EllipseType
        {
            get => _ellipseType;
            set => SetProperty(ref _ellipseType, value);
        }

        public bool IsClockwise
        {
            get => _isClockwise;
            set => SetProperty(ref _isClockwise, value);
        }

        public float Depth
        {
            get => _depth;
            set => SetProperty(ref _depth, value);
        }

        public float Thickness
        {
            get => _thickness;
            set => SetProperty(ref _thickness, value);
        }

        public float Diameter
        {
            get => _diameter;
            set => SetProperty(ref _diameter, value);
        }

        public float TopDiameter
        {
            get => _topDiameter;
            set => SetProperty(ref _topDiameter, value);
        }

        public float BottomDiameter
        {
            get => _bottomDiameter;
            set => SetProperty(ref _bottomDiameter, value);
        }

        public Vector3D LineStart
        {
            get => _lineStart;
            set => SetProperty(ref _lineStart, value);
        }

        public Vector3D LineEnd
        {
            get => _lineEnd;
            set => SetProperty(ref _lineEnd, value);
        }
    }
}