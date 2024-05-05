using System;

namespace Adamantium.Fonts.Tables;

internal class OS2Table
{
    public UInt16 version;
    public Int16 xAvgCharWidth;
    public UInt16 usWeightClass;
    public UInt16 usWidthClass;
    public UInt16 fsType;
    public Int16 ySubscriptXSize;
    public Int16 ySubscriptYSize;
    public Int16 ySubscriptXOffset;
    public Int16 ySubscriptYOffset;
    public Int16 ySuperscriptXSize;
    public Int16 ySuperscriptYSize;
    public Int16 ySuperscriptXOffset;
    public Int16 ySuperscriptYOffset;
    public Int16 yStrikeoutSize;
    public Int16 yStrikeoutPosition;
    public Int16 sFamilyClass;
    public byte[] panose; // array of 10 bytes 	
    public UInt32 ulUnicodeRange1;
    public UInt32 ulUnicodeRange2;
    public UInt32 ulUnicodeRange3;
    public UInt32 ulUnicodeRange4;
    public string achVendID;	// 32 bytes (4 items, each - 8 bytes) 
    public UInt16 fsSelection;
    public UInt16 usFirstCharIndex;
    public UInt16 usLastCharIndex;
    public Int16 sTypoAscender;
    public Int16 sTypoDescender;
    public Int16 sTypoLineGap;
    public UInt16 usWinAscent;
    public UInt16 usWinDescent;
    public UInt32 ulCodePageRange1;
    public UInt32 ulCodePageRange2;
    public Int16 sxHeight;
    public Int16 sCapHeight;
    public UInt16 usDefaultChar;
    public UInt16 usBreakChar;
    public UInt16 usMaxContext;
    public UInt16 usLowerOpticalPointSize;
    public UInt16 usUpperOpticalPointSize;
}