using System;
using System.Diagnostics;

namespace Adamantium.Imaging.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Started gif loading");
            var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\gif\infinity.gif");
            //var img = Image.Load(@"m:\AdamantiumProject\Adamantium\Tests\TestAssets\ColoredImage.jpg");
            Console.WriteLine("Start saving gif");
            var timer = Stopwatch.StartNew();
            img?.Save("test.gif", ImageFileType.Gif);
            img?.Dispose();
            Console.WriteLine("Finished saving gif");
            timer.Stop();
            Console.WriteLine($"Elapsed milliseconds = {timer.ElapsedMilliseconds}");
            Console.ReadKey();
        }
    }
}
