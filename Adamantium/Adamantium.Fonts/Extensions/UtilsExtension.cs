namespace Adamantium.Fonts.Extensions
{
    internal static class UtilsExtension
    {
        public static float ToF2Dot14(this short value)
        {
            return (value / 16384.0f);
        }
    }
}