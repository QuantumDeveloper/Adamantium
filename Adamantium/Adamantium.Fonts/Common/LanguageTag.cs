using System;

namespace Adamantium.Fonts.Common
{
    public readonly struct LanguageTag
    {
        public readonly string Tag;
        public readonly string FriendlyName;
        public readonly string[] ISONames;

        public LanguageTag(string tag, string friendlyName, params string[] isoNames)
        {
            Tag = tag;
            FriendlyName = friendlyName;
            ISONames = new string[isoNames.Length];
            for (int i = 0; i < isoNames.Length; ++i)
            {
                ISONames[i] = isoNames[i];
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Tag, FriendlyName);
        }

        public override string ToString()
        {
            return $"Tag: {Tag} Name: {FriendlyName}";
        }
    }
}