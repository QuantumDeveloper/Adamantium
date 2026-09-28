using System;

namespace Adamantium.Imaging.Jpeg.Decoder;

/// <summary>Thrown by <see cref="JpegDecoder.DecodePreview"/> when this particular picture cannot be previewed at an
/// eighth of its size - currently, when its dimensions are not a whole number of MCU blocks. The caller decodes it
/// normally instead; the preview is an optimisation, and its absence is never a failure of the decode itself.</summary>
public class PreviewNotAvailableException : Exception
{
    public PreviewNotAvailableException(string message) : base(message)
    {
    }
}
