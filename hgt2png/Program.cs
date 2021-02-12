using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace hgt2png
{
    class Program
    {
        public static unsafe Bitmap Hgt2Png<T>(T[] hgt, int size, PixelFormat format, T max) where T : unmanaged
        {
            var res = new Bitmap(size, size, format);
            var hgtTriple = new T[hgt.Length * 4];
            for (int i = 0; i < hgt.Length; i++)
            {
                hgtTriple[i * 4 + 0] = hgtTriple[i * 4 + 1] = hgtTriple[i * 4 + 2] = hgt[i];
                hgtTriple[i * 4 + 3] = max;
            }
            fixed (T* dotsBytes = hgtTriple)
            {
                var resData = res.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, format);
                Buffer.MemoryCopy(dotsBytes, (void*)resData.Scan0, hgtTriple.Length * sizeof(T), hgtTriple.Length * sizeof(T));
                res.UnlockBits(resData);
            }
            return res;
        }

        public static unsafe ushort[] Bytes2UShort(byte[] bytes)
        {
            var ushorts = new ushort[bytes.Length / 2];
            bytes = bytes.Reverse().ToArray();
            fixed (void* bytesPtr = bytes, ushortsPtr = ushorts)
                Buffer.MemoryCopy(bytesPtr, ushortsPtr, bytes.Length, bytes.Length);
            return ushorts.Reverse().ToArray();
        }

        private static bool BadDot(ushort dot)
            => dot / 256 == 128 || dot / 256 == 255;

        public static ushort CollectAround(ushort[] bytes, int currId, int size)
        {
            var sum = 0;
            var count = 0;

            if (currId - 1 >= 0 && !BadDot(bytes[currId - 1]))
            {
                count++;
                sum += bytes[currId - 1];
            }

            if (currId + 1 < bytes.Length && !BadDot(bytes[currId + 1]))
            {
                count++;
                sum += bytes[currId + 1];
            }

            if (currId - size >= 0 && !BadDot(bytes[currId - size]))
            {
                count++;
                sum += bytes[currId - size];
            }

            if (currId + size < bytes.Length && !BadDot(bytes[currId + size]))
            {
                count++;
                sum += bytes[currId + size];
            }

            if (count == 0)
                return 0;
            return (ushort)(sum / count);
        }

        public static int InterpolateBrokenDots(ushort[] dots, int size)
        {
            var interCount = 0;
            for (int i = 0; i < dots.Length; i++)
                if (BadDot(dots[i]))
                {
                    dots[i] = CollectAround(dots, i, size);
                    interCount++;
                }
            return interCount;
        }

        public static int MaxFirstByte(ushort[] ushorts)
        {
            var m = -1;
            foreach (var c in ushorts)
                if (c / 256 > m)
                    m = c / 256;
            return m;
        }

        public static byte[] UShorts2ByteCompress(ushort[] ushorts, int div)
            => ushorts.Select(c => (byte)(c / div)).ToArray();

        public static string FindArgValue(string[] args, string arg)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == arg)
                    return args[i + 1];
            return null;
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Input the path to hgt (example: D:/N44E033.hgt): ");
                var pathFromLocal = Console.ReadLine();
                Console.WriteLine("Input the directory and prefix for the final images (example: D:/res): ");
                var prefixPathToLocal = Console.ReadLine();
                Console.WriteLine("Input other params");
                args = new[] { pathFromLocal, prefixPathToLocal }.Concat(Console.ReadLine().Split(" ")).ToArray();
            }

            if (args[0].EndsWith(".exe") || args[0].EndsWith(".csproj"))
                args = args[1..];

            var pathFrom = args[0];
            var prefixPathTo = args[1];

            Console.WriteLine("Starting processing...");

            var pars = Enumerable.Range(2, args.Length - 2).Select(c => args[c]);
            var bytes = File.ReadAllBytes(pathFrom);

            var len = (int)Math.Sqrt(bytes.Length / 2);
            Console.WriteLine($"Fragment size: {len}x{len}px");

            var ushorts = Bytes2UShort(bytes);
            
            Console.WriteLine(InterpolateBrokenDots(ushorts, len) + " broken dots were interpolated");
            var highestByte = MaxFirstByte(ushorts);
            Console.WriteLine("Max first byte: " + highestByte);

            if (FindArgValue(args, "-maxbyte") is { } strMaxByte && int.TryParse(strMaxByte, out var maxByte))
            {
                Console.WriteLine("Manual adjusting mode");

                var goodUserBytes = UShorts2ByteCompress(ushorts, maxByte + 1);
                var pathUser32 = prefixPathTo + $"_32bit_maxbyte_{maxByte}.png";
                Hgt2Png(goodUserBytes, len, PixelFormat.Format32bppRgb, byte.MaxValue).Save(pathUser32);
                Console.WriteLine("32-bit version saved to " + pathUser32);

                var pathUser64 = prefixPathTo + $"_64bit_lightened_maxbyte_{maxByte}.png";
                Hgt2Png(ushorts.Select(c => (ushort)(c * 256 / (maxByte + 1))).ToArray(), len, PixelFormat.Format64bppArgb, ushort.MaxValue).Save(pathUser64);
                Console.WriteLine("64-bit lightened version saved to " + pathUser64);
            }
            else
            {
                Console.WriteLine("Auto adjusting mode");

                var goodBytes = UShorts2ByteCompress(ushorts, highestByte + 1);
                var path32 = prefixPathTo + $"_32bit_maxbyte_{highestByte}.png";
                Hgt2Png(goodBytes, len, PixelFormat.Format32bppArgb, byte.MaxValue).Save(path32);
                Console.WriteLine("32-bit version saved to " + path32);

                var path64 = prefixPathTo + "_64bit.png";
                Hgt2Png(ushorts, len, PixelFormat.Format64bppArgb, ushort.MaxValue).Save(path64);
                Console.WriteLine("64-bit version saved to " + path64);
            }
        }
    }
}
