using System;
using System.Security.Cryptography;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// UID Generator
    /// </summary>
    public class UidGenerator
    {
        private static object syncObject = new object();

        private static readonly RandomNumberGenerator RandomGenerator = RandomNumberGenerator.Create();
        private static int length = 15;

        /// <summary>
        /// Generate unique int64 number that can be used as UID
        /// </summary>
        /// <returns></returns>
        public static Int64 Generate()
        {
            lock (syncObject)
            {
                var randomData = new byte[length];
                RandomGenerator.GetNonZeroBytes(randomData);
                return BitConverter.ToInt64(randomData, 0);
            }
        }
    }
}
