namespace Adamantium.Fonts.OTF
{
    internal class RegionAxisCoordinates
    {
        public float StartCoord { get; set; }
        
        public float PeakCoord { get; set; }
        
        public float EndCoord { get; set; }

        public override string ToString()
        {
            return $"Start: {StartCoord} Peak: {PeakCoord} End {EndCoord}";
        }
    }
}