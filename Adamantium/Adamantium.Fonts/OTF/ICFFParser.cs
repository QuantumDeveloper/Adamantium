namespace Adamantium.Fonts.OTF
{
    internal interface ICFFParser
    {
        CFFIndex GlobalSubroutineIndex { get; }
        
        int GlobalSubrBias { get; }
        CFFFont Parse();
    }
}