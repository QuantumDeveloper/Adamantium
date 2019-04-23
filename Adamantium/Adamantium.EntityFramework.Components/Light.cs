using System;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components
{
    public class Light: ActivatableComponent
    {
        private Vector3F _color;
        private float _intensity;
        private LightType _type;
        private float _range;
        private bool _isWithShadows;
        private Vector3F _direction;
        private float _outerSpotAngle;
        private float _innerSpotAngle;
        private float _innerAngleDelta;
        private float _outerAngleDelta;

        public Light() : this(LightType.Point)
        {
        }

        public Light(LightType lightType)
        {
            Type = lightType;
            Range = 1f;
            OuterSpotAngle = (float)Math.Atan(Range);
            Intensity = 1.0f;
            Color = Colors.White.ToVector3();
            Direction = Vector3F.Down;
        }

        public Vector3F Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public Single Intensity
        {
            get => _intensity;
            set => SetProperty(ref _intensity, value);
        }

        public LightType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public Single Range
        {
            get => _range;
            set => SetProperty(ref _range, value);
        }

        public Boolean IsWithShadows
        {
            get => _isWithShadows;
            set => SetProperty(ref _isWithShadows, value);
        }

        public Int32 ShadowMapResolution { get; set; }

        public Single DepthBias => 1.0f / (20 * Range);

        public Vector3F Direction
        {
            get => _direction;
            set => SetProperty(ref _direction, value);
        }

        public float OuterSpotAngle
        {
            get => _outerSpotAngle;
            set => SetProperty(ref _outerSpotAngle, value);
        }

        public float SpotRadius => Range * (float)Math.Tan(OuterSpotAngle);

        public float InnerAngleDelta
        {
            get => _innerAngleDelta;
            set => SetProperty(ref _innerAngleDelta, value);
        }

        public float OuterAngleDelta
        {
            get => _outerAngleDelta;
            set => SetProperty(ref _outerAngleDelta, value);
        }

        public float InnerSpotAngle
        {
            get => _innerSpotAngle;
            set => SetProperty(ref _innerSpotAngle, value);
        }
    }
}
