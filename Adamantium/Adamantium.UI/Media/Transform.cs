using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public class Transform : AdamantiumComponent
{
    public static readonly AdamantiumProperty ScaleXProperty = AdamantiumProperty.Register(nameof(ScaleX),
        typeof (Double), typeof (Transform), new PropertyMetadata(1.0, TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty ScaleYProperty = AdamantiumProperty.Register(nameof(ScaleY),
        typeof (Double), typeof (Transform), new PropertyMetadata(1.0, TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationAngleProperty = AdamantiumProperty.Register(nameof(RotationAngle),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationCenterXProperty = AdamantiumProperty.Register(nameof(RotationCenterX),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationCenterYProperty = AdamantiumProperty.Register(nameof(RotationCenterY),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
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

    public Double ScaleX
    {
        get => GetValue<Double>(ScaleXProperty);
        set => SetValue(ScaleXProperty, value);
    }
        
    public Double ScaleY
    {
        get => GetValue<Double>(ScaleYProperty);
        set => SetValue(ScaleYProperty, value);
    }
        
    public Double RotationAngle
    {
        get => GetValue<Double>(RotationAngleProperty);
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
        
    public Matrix4x4 Matrix { get; private set; }

    private void UpdateTransform()
    {
        Matrix = CalculateFinalTransform();
    }
        
    private Matrix4x4 CalculateFinalTransform()
    {
        var scaling = new Vector3(ScaleX, ScaleY);
        var translation = new Vector3((float)TranslateX, (float)TranslateY, 0);
        var rotation = Quaternion.RotationAxis(Vector3.UnitZ, MathHelper.DegreesToRadians(RotationAngle));
        var rotationCenter = new Vector3((float)RotationCenterX, (float)RotationCenterY, 0);
        var scalingCenter = Vector3.Zero;
        var scalingRotation = Quaternion.Identity;

        Matrix4x4.Transformation(
            ref scalingCenter, 
            ref scalingRotation, 
            ref scaling, 
            ref rotationCenter,
            ref rotation, 
            ref translation, 
            out var matrix);

        return matrix;
    }
}