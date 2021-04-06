﻿namespace Adamantium.Fonts.OTF
{
    public enum OperatorsType : ushort
    {
        // reserved         = 0
        hstem               = 1,
        // reserved         = 2
        vstem               = 3,
        vmoveto             = 4, /**/
        rlineto             = 5, /**/
        hlineto             = 6, /**/
        vlineto             = 7, /**/
        rrcurveto           = 8, /**/
        // reserved         = 9
        //closepath         = 9
        callsubr            = 10, /**/
        @return             = 11, /**/
        //reserved          = 12 0
        //dotsection        = (12 << 8) | 0,
        //reserved          = 12 1
        //vstem3            = (12 << 8) | 1,
        //reserved          = 12 2
        //hstem3            = (12 << 8) | 2,
        and                 = (12 << 8) | 3, /**/
        or                  = (12 << 8) | 4, /**/
        not                 = (12 << 8) | 5, /**/
        //reserved          = 12 6
        //seac              = (12 << 8) | 6,
        //reserved          = 12 7
        //sbw               = (12 << 8) | 7,
        //reserved          = 12 8
        abs                 = (12 << 8) | 9, /**/
        add                 = (12 << 8) | 10, /**/
        sub                 = (12 << 8) | 11, /**/
        div                 = (12 << 8) | 12, /**/
        //reserved          = 12 13
        neg                 = (12 << 8) | 14, /**/
        eq                  = (12 << 8) | 15, /**/
        //reserved          = 12 16
        //callothersubr     = (12 << 8) | 16,
        //reserved          = 12 17
        //pop               = (12 << 8) | 17,
        drop                = (12 << 8) | 18, /**/
        //reserved          = 12 19
        put                 = (12 << 8) | 20,
        get                 = (12 << 8) | 21,
        ifelse              = (12 << 8) | 22, /**/
        random              = (12 << 8) | 23, /**/
        mul                 = (12 << 8) | 24, /**/
        //reserved          = 12 25
        sqrt                = (12 << 8) | 26, /**/
        dup                 = (12 << 8) | 27, /**/
        exch                = (12 << 8) | 28, /**/
        index               = (12 << 8) | 29,
        roll                = (12 << 8) | 30,
        //reserved          = 12 31
        //reserved          = 12 32
        //reserved          = 12 33
        //setcurrentpoint   = (12 << 8) | 33,
        hflex               = (12 << 8) | 34,
        flex                = (12 << 8) | 35,
        hflex1              = (12 << 8) | 36,
        flex1               = (12 << 8) | 37,
        // reserved         = 13,
        //hsbw              = 13,
        endchar             = 14, /**/
        vsindex             = 15,
        blend               = 16,
        // reserved         = 17,
        hstemhm             = 18, /**/
        hintmask            = 19, /**/
        cntrmask            = 20, /**/
        rmoveto             = 21, /**/
        hmoveto             = 22, /**/
        vstemhm             = 23, /**/
        rcurveline          = 24, /**/
        rlinecurve          = 25, /**/
        vvcurveto           = 26, /**/
        hhcurveto           = 27, /**/
        callgsubr           = 29, /**/
        vhcurveto           = 30, /**/
        hvcurveto           = 31 /**/
    };
}
