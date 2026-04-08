using WalkerGlobe2.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace WalkerGlobe2
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            var textureFile = WalkerGlobe.DefaultDayTexturePath;

            if (!File.Exists(textureFile))
            {
                Console.WriteLine($"Texture not found: {textureFile}");
                return;
            }

            using (var globe = new WalkerGlobe2.WalkerGlobe(textureFile, true))
            {
                Console.WriteLine($"[{sw.ElapsedMilliseconds,5}ms] Globe constructor");
                globe.Run(30.0);
            }
        }
    }
}
