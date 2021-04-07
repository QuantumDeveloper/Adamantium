namespace Adamantium.Fonts.OTF
{
    internal class CIDFontInfo
    {
        public string ROS_Register;
        public string ROS_Ordering;
        public string ROS_Supplement;

        public double CIDFontVersion;
        public int CIDFountCount;
        public int FDSelect;
        public int FDArray;

        public int FdSelectFormat;
        public byte[] FdRanges0;
        public FDRange[] FdRanges;
    }
}