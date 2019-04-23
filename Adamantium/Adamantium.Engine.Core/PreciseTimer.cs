using System;
using System.Diagnostics;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// Class, which implements high precision timer from C++
    /// </summary>
    public class PreciseTimer
    {
        public const int MillisecondsInSecond = 1000;

        private long previousElapsedTime = 0;
        private readonly Stopwatch qpc;

        /// <summary>
        /// Creates precise timer instance
        /// </summary>
        public PreciseTimer()
        {
            qpc = Stopwatch.StartNew();
            GetElapsedTime();//Get rid of first incorrect result
        }

        /// <summary>
        /// Get elapsed time
        /// </summary>
        /// <returns>frame time in seconds</returns>
        public Double GetElapsedTime()
        {
            long time = 0;
            time = qpc.ElapsedMilliseconds;
            double elapsedTime = (double)(time - previousElapsedTime) / MillisecondsInSecond;
            previousElapsedTime = time;
            return elapsedTime;
        }
    }
}

