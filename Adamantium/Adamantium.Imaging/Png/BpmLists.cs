namespace Adamantium.Imaging.Png
{
    /// <summary>
    /// Lists of chains
    /// </summary>
    internal class BpmLists
    {
        /*memory pool*/
        public uint MemSize { get; set; }
        public BPMNode[] Memory { get; set; }
        public uint Numfree { get; set; }
        public uint NextFree { get; set; }
        public BPMNode[] FreeList { get; set; }
        /*two heads of lookahead chains per list*/
        public uint ListSize { get; set; }
        public BPMNode[] Chains0 { get; set; }
        public BPMNode[] Chains1 { get; set; }
    }
}
