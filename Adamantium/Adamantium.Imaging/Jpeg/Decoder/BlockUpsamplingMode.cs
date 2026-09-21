namespace Adamantium.Imaging.Jpeg.Decoder;

public enum BlockUpsamplingMode
{
    /// <summary> The simplest upsampling mode. Produces sharper edges. </summary>
    BoxFilter,
    /// <summary> Smoother upsampling. May improve color spread for some images. </summary>
    Interpolate
}