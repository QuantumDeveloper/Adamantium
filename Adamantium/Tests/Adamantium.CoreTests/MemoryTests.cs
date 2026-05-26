using System;
using System.Diagnostics;
using NUnit.Framework;
using QuantumBinding.Utils;

namespace Adamantium.CoreTests;

public class MemoryTests
{
    [Test]
    public unsafe void AllocMemoryWithNativeMemory()
    {
        string str = "Hello Comrade General";
        var timer1 = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var ptr = NativeUtils.StringToPointer(str, true);
            NativeUtils.Free(ptr);
        }
        timer1.Stop();
        
        var timer2 = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            NativeUtils.ExecuteWithUtf8String(str, _ =>
            {
                
            });
        }
        timer2.Stop();
        Console.WriteLine(timer1.ElapsedMilliseconds + "ms");
        Console.WriteLine(timer2.ElapsedMilliseconds + "ms");
    }
}