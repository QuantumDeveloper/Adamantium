﻿using System;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers.Pixels
{
    public interface IGenericPixel
    {
        // components
        Int32 Alpha { get; }
        Int32 Red { get; }
        Int32 Green { get; }
        Int32 Blue { get; }

        // higher-level values
        Int32 Rgba { get; }
        UInt64 Value { get; set; }

        // color methods
        Color GetColor();
        void SetColor(Color color);
    }
}
