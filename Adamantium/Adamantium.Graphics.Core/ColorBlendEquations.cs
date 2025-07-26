using AdamantiumVulkan.Core;

namespace Adamantium.Graphics.Core;

public static class ColorBlendEquations
{
    public static ColorBlendEquationEXT Opaque {get; private set;}
    
    public static ColorBlendEquationEXT Fonts {get; private set;}
    public static ColorBlendEquationEXT Fonts2 {get; private set;}
    
    public static ColorBlendEquationEXT AlphaBlend {get; private set;}
    
    public static ColorBlendEquationEXT LightMap {get; private set;}
    
    public static ColorBlendEquationEXT Additive {get; private set;}
    
    public static ColorBlendEquationEXT NonPremultiplied {get; private set;}

    public static ColorBlendEquationEXT Default => Opaque;

    static ColorBlendEquations()
    {
        Opaque = New(BlendFactor.One, BlendFactor.Zero, BlendOp.Add, BlendOp.Add);
        Fonts = New(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, BlendOp.Subtract);
        Fonts2 = New2();
        AlphaBlend = New(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, BlendOp.Add);
        LightMap = New(BlendFactor.One, BlendFactor.One, BlendOp.Add, BlendOp.Add);
        Additive = New(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add, BlendOp.Add);
        NonPremultiplied = New(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, BlendOp.Add);
    }

    private static ColorBlendEquationEXT New(
        BlendFactor sourceBlend, 
        BlendFactor destinationBlend, 
        BlendOp colorBlendOp, 
        BlendOp alphaBlendOp,
        BlendFactor srcAlpha = BlendFactor.One, 
        BlendFactor dstAlpha = BlendFactor.Zero)
    {
        var equation = new ColorBlendEquationEXT();
        equation.SrcColorBlendFactor = sourceBlend;
        equation.DstColorBlendFactor = destinationBlend;
        equation.ColorBlendOp = colorBlendOp;
        
        equation.SrcAlphaBlendFactor = srcAlpha;
        equation.DstAlphaBlendFactor = dstAlpha;
        equation.AlphaBlendOp = alphaBlendOp;

        return equation;
    }
    
    private static ColorBlendEquationEXT New2()
    {
        var equation = new ColorBlendEquationEXT();
        equation.SrcColorBlendFactor = BlendFactor.SrcAlpha;
        equation.SrcAlphaBlendFactor = BlendFactor.One;
        equation.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
        equation.DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha;
        equation.ColorBlendOp = BlendOp.Add;
        equation.AlphaBlendOp = BlendOp.Add;

        return equation;
    }
}