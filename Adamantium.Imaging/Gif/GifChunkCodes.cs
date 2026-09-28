namespace Adamantium.Imaging.Gif
{
    enum GifChunkCodes : byte
    {
        None = 0x0,
        PlainTextExtension = 0x01,
        ExtensionIntroducer = 0x21,
        ImageDescriptor = 0x2C,
        Trailer = 0x3B,
        GraphicControl = 0xF9,
        ApplicationExtension = 0xFF,
        CommentExtension = 0xFE
    }
}
