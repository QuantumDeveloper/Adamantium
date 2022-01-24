using System;
using System.Linq;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public class Nurbs : BSpline
{
    public Nurbs()
    {
        
    }
    
    public static readonly AdamantiumProperty IsUniformProperty =
        AdamantiumProperty.Register(nameof(IsUniform), typeof(bool), typeof(Nurbs),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty UseCustomDegreeProperty =
        AdamantiumProperty.Register(nameof(UseCustomDegree), typeof(bool), typeof(Nurbs),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));
    
    public static readonly AdamantiumProperty CustomDegreeProperty =
        AdamantiumProperty.Register(nameof(CustomDegree), typeof(Int32), typeof(Nurbs),
            new PropertyMetadata(default(Int32), PropertyMetadataOptions.AffectsRender, CustomDegreeCoerceCallback));

    private static object CustomDegreeCoerceCallback(AdamantiumComponent a, object baseValue)
    {
        var customDegree = (Int32)baseValue;
        if (a is Nurbs nurbs)
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
    
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var degree = Points.Count - 1;
        if (UseCustomDegree)
        {
            degree = CustomDegree;
        }

        var streamContext = StreamGeometry.Open();
        streamContext.BeginFigure(Points[0], true, true).NurbsTo(Points.Skip(1), IsUniform, UseCustomDegree, degree, true);
            
        context.BeginDraw(this);
        
        context.DrawGeometry(Stroke, StreamGeometry, GetPen());
        context.EndDraw(this);
    }
}