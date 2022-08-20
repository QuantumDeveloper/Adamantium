using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class RegionData
    {
        public List<double> Data;

        public RegionData()
        {
            Data = new List<double>();
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var other = obj as RegionData;
            if (Data.Count != other.Data.Count) return false;

            for (int i = 0; i < Data.Count; i++)
            {
                double d = Data[i];
                if (d != other.Data[i]) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            foreach(var d in Data)
            {
                hashCode += (int)d ^ 377;

            }

            return hashCode;
        }
    }
}