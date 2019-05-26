namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class pHYs: Chunk
    {
        public pHYs()
        {
            Name = "pHYs";
        }

        public uint PhysX { get; set; }
        public uint PhysY { get; set; }
        public Unit Unit { get; set; }
    }
}