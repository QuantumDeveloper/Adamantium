using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media
{
    public class Transform : AdamantiumComponent
    {
        public static readonly AdamantiumProperty ScaleXProperty = AdamantiumProperty.Register(nameof(ScaleX),
            typeof (Single), typeof (Transform), new PropertyMetadata(1.0f, TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty ScaleYProperty = AdamantiumProperty.Register(nameof(ScaleY),
            typeof (Single), typeof (Transform), new PropertyMetadata(1.0f, TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty RotationAngleProperty = AdamantiumProperty.Register(nameof(RotationAngle),
            typeof (Single), typeof (Transform), new PropertyMetadata(default(Single), TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty RotationCenterXProperty = AdamantiumProperty.Register(nameof(RotationCenterX),
            typeof (Single), typeof (Transform), new PropertyMetadata(default(Single), TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty RotationCenterYProperty = AdamantiumProperty.Register(nameof(RotationCenterY),
            typeof (Single), typeof (Transform), new PropertyMetadata(default(Single), TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty TranslateXProperty = AdamantiumProperty.Register(nameof(TranslateX),
            typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
        public static readonly AdamantiumProperty TranslateYProperty = AdamantiumProperty.Register(nameof(TranslateY),
            typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));

        private static void TransformPropertyChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            if (a is Transform transform)
            {
                transform.UpdateTransform();
            }
        }

        public Single ScaleX
        {
            get => GetValue<Single>(ScaleXProperty);
            set => SetValue(ScaleXProperty, value);
        }
        
        public Single ScaleY
        {
            get => GetValue<Single>(ScaleYProperty);
            set => SetValue(ScaleYProperty, value);
        }
        
        public Single RotationAngle
        {
            get => GetValue<Single>(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
        }
        
        public Double TranslateX
        {
            get => GetValue<Double>(TranslateXProperty);
            set => SetValue(ScaleXProperty, value);
        }
        
        public Double TranslateY
        {
            get => GetValue<Double>(TranslateYProperty);
            set => SetValue(ScaleYProperty, value);
        }
        
        public Double RotationCenterX
        {
            get => GetValue<Double>(RotationCenterXProperty);
            set => SetValue(RotationCenterXProperty, value);
        }
        
        public Double RotationCenterY
        {
            get => GetValue<Double>(RotationCenterYProperty);
            set => SetValue(RotationCenterYProperty, value);
        }
        
        public Matrix4x4F Matrix { get; private set; }

        private void UpdateTransform()
        {
            Matrix = CalculateFinalTransform();
        }
        
        private Matrix4x4F CalculateFinalTransform()
        {
            Matrix4x4F matrix;
            var scaling = new Vector3F(ScaleX, ScaleY);
            var translation = new Vector3F((float)TranslateX, (float)TranslateY, 0);
            var rotation = QuaternionF.RotationAxis(Vector3F.UnitZ, RotationAngle);
            var rotationCenter = new Vector3F((float)RotationCenterX, (float)RotationCenterY, 0);
            var scalingCenter = Vector3F.Zero;
            var scalingRotation = QuaternionF.Identity;

            Matrix4x4F.Transformation(
                ref scalingCenter, 
                ref scalingRotation, 
                ref scaling, 
                ref rotationCenter,
                ref rotation, 
                ref translation, 
                out matrix);

            return matrix;
        }
    }
}