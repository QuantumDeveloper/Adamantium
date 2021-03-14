namespace Adamantium.Fonts.OTF
{
    public struct OutlinePoint
    {
        public double X;
        public double Y;
        public bool IsControl;

        public OutlinePoint(float x, float y, bool control = false)
        {
            X = x;
            Y = y;
            IsControl = control;
        }

        public override string ToString()
        {
            return $"X: {X}, Y: {Y}, IsControl: {IsControl}";
        }
    }
}
