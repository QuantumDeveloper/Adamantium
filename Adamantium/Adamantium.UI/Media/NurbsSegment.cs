using System;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media;

public class NurbsSegment : BSplineSegment
{
    public static readonly AdamantiumProperty IsUniformProperty =
        AdamantiumProperty.Register(nameof(IsUniform), typeof(bool), typeof(NurbsSegment),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty UseCustomDegreeProperty =
        AdamantiumProperty.Register(nameof(UseCustomDegree), typeof(bool), typeof(NurbsSegment),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty CustomDegreeProperty =
        AdamantiumProperty.Register(nameof(CustomDegree), typeof(Int32), typeof(NurbsSegment),
            new PropertyMetadata(default(Int32), PropertyMetadataOptions.AffectsRender, CustomDegreeCoerceCallback));

    private static object CustomDegreeCoerceCallback(AdamantiumComponent a, object baseValue)
    {
        var customDegree = (Int32)baseValue;
        if (a is NurbsSegment nurbs)
        {
            return nurbs.CheckCustomDegree(customDegree);
        }

        return baseValue;
    }

    protected override void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
    {
        CustomDegree = CheckCustomDegree(CustomDegree);
        base.OnPropertyChanged(e);
    }

    public bool IsUniform
    {
        get => GetValue<bool>(IsUniformProperty);
        set => SetValue(IsUniformProperty, value);
    }
    
    public bool UseCustomDegree
    {
        get => GetValue<bool>(UseCustomDegreeProperty);
        set => SetValue(UseCustomDegreeProperty, value);
    }
    
    public Int32 CustomDegree
    {
        get => GetValue<Int32>(CustomDegreeProperty);
        set => SetValue(CustomDegreeProperty, value);
    }

    private Int32 CheckCustomDegree(Int32 currentDegree)
    {
        if (Points == null) return currentDegree;

        if (currentDegree < 0)
        {
            return 0;
        }
        else if (currentDegree > Points.Count)
        {
            return Points.Count;
        }

        return currentDegree;
    }

    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var points = new PointsCollection();
        points.Add(currentPoint);
        points.AddRange(Points);
        var degree = points.Count - 1;
        if (UseCustomDegree)
        {
            degree = CustomDegree;
        }
        
        return MathHelper.GetNurbsCurve(points, degree, IsUniform, 1.0/SampleRate);
    }

    
}