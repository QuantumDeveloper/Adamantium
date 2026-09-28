using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal interface ICFFParser
    {
        CFFIndex GlobalSubroutineIndex { get; }
        
        int GlobalSubrBias { get; }
        CFFFont Parse();
    }
}