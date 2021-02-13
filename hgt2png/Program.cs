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
        public static int MaxFirstByte(ushort[] ushorts)
        {
            var m = -1;
            foreach (var c in ushorts)
                if (c / 256 > m)
                    m = c / 256;
            return m;
        }

        public static string FindArgValue(string[] args, string arg)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == arg)
                    return args[i + 1];
            return null;
        }

        static string PlainToCoords(int x, int y)
        {
            var A = "S";
            if (x > 90)
            {
                x -= 90;
                A = "N";
            }
            else
                x = 90 - x;
            var B = "E";
            if (y > 180)
            {
                y -= 180;
                B = "W";
            }
            else
                y = 180 - y;
            if (y <= 99)
                B += "0";
            if (y <= 9)
                B += "0";
            return A + x.ToString() + B + y.ToString();
        }

        public static IEnumerable<ushort[]> Iterate(ushort[,][] arr)
        {
            for (int x = 0; x < arr.GetLength(0); x++)
                for (int y = 0; y < arr.GetLength(1); y++)
                    yield return arr[x, y];
        }

        public static void CopyFragTo<T>(Bitmap dstBmp, Bitmap srcBmp, int xFrom, int yFrom) where T : unmanaged
        {
            using var dst = new BmpRaw<T>(dstBmp, format: dstBmp.PixelFormat);
            using var src = new BmpRaw<T>(srcBmp, format: srcBmp.PixelFormat);
            for (int x = 0; x < srcBmp.Width; x++)
                for (int y = 0; y < srcBmp.Height; y++)
                    dst[x + xFrom, y + yFrom] = src[x, y];
        }

        static void Log(string s)
        {
            Console.WriteLine(s);
        }

        static void LogInplace(string s)
        {
            Console.Write(s);
        }

        static string[] GatherMissingArgs()
        {
            Log(@"Welcome to hgt2png!
Please, input the path to the directory, storing your .hgt files (e. g. N44E033.hgt)");
            LogInplace("Path: ");
            var mPath = Console.ReadLine();
            Log("Input the destination path and the prefix (e. g. D:/res), or press enter to skip");
            LogInplace("Path prefix: ");
            var dstPath = Console.ReadLine();
            if (dstPath == "")
                return new[] { mPath };
            Log("Input maxbyte, or press enter to skip");
            LogInplace("maxbyte: ");
            var maxByte = Console.ReadLine();
            if (maxByte == "")
                return new[] { mPath, dstPath };
            return new[] { mPath, dstPath, "-maxbyte", maxByte };
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
                args = GatherMissingArgs();

            if (args[0].EndsWith(".exe") || args[0].EndsWith(".csproj"))
                args = args[1..];

            var pathFrom = args[0];
            var prefixPathTo = args.Length > 1 ? args[1] : Path.Join(pathFrom, "res");

            Log("Starting processing...");

            Log($"Looking for files in {pathFrom}");

            var matrix = new ushort[180, 360][];

            for (int x = 0; x < 180; x++)
                for (int y = 0; y < 360; y++)
                {
                    var name = PlainToCoords(x, y) + ".hgt";
                    var path = Path.Join(pathFrom, name);
                    if (File.Exists(path))
                    {
                        Log($"Found {name}");
                        matrix[x, y] = Convert.Bytes2UShort(File.ReadAllBytes(path));
                    }
                }

            var notNulls = Iterate(matrix).Where(c => c is not null);

            if (!notNulls.Any())
            {
                Log("No fragments found");
                return;
            }

            var any = notNulls.First();
            var size = (int)Math.Sqrt(any.Length);

            var interpolatedCount = 0;

            foreach (var frag in notNulls)
                interpolatedCount = Interpolate.InterpolateBrokenDots(frag, size);

            Log($"Interpolated dots in total: {interpolatedCount}");

            if (!(FindArgValue(args, "-maxbyte") is { } strMaxByte) || !int.TryParse(strMaxByte, out var maxByte))
            {
                maxByte = notNulls.Select(c => MaxFirstByte(c)).Max();
                Log($"Auto max byte: {maxByte}");
            }

            int minX = 400, minY = 400, maxX = -1, maxY = -1;
            for (int x = 0; x < 180; x++)
                for (int y = 0; y < 360; y++)
                    if (matrix[x, y] is not null)
                    {
                        if (x < minX)
                            minX = x;
                        if (y < minY)
                            minY = y;
                        if (x > maxX)
                            maxX = x;
                        if (y > maxY)
                            maxY = y;
                    }

            var res32 = new Bitmap((maxY - minY + 1) * size, (maxX - minX + 1) * size, PixelFormat.Format32bppArgb);
            var res64 = new Bitmap((maxY - minY + 1) * size, (maxX - minX + 1) * size, PixelFormat.Format64bppArgb);

            Log($"Final resolution: {res32.Width}x{res32.Height}");

            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    if (matrix[x, y] is { } frag)
                    {
                        var bytesFor32 = Convert.UShorts2ByteCompress(frag, maxByte + 1);
                        var bytesFor64 = frag.Select(c => (ushort)(c * 256 / (maxByte + 1))).ToArray();
                        var frag32 = Hgt2PngConverter.Hgt2Png(bytesFor32, size, PixelFormat.Format32bppArgb, byte.MaxValue);
                        var frag64 = Hgt2PngConverter.Hgt2Png(bytesFor64, size, PixelFormat.Format64bppArgb, ushort.MaxValue);
                        CopyFragTo<(byte, byte, byte, byte)>(res32, frag32, (maxY - y) * size, (maxX - x) * size);
                        CopyFragTo<(ushort, ushort, ushort, ushort)>(res64, frag64, (maxY - y) * size, (maxX - x) * size);
                        Log($"Processed {x} {y}");
                    }

            Log("Saving...");

            var finalPathNoBits = prefixPathTo + PlainToCoords(minX, minY) + "-" + PlainToCoords(maxX, maxY);
            res32.Save(finalPathNoBits + "_32bit.png");
            res64.Save(finalPathNoBits + "_64bit.png");

            Log("Done");
        }
    }
}
