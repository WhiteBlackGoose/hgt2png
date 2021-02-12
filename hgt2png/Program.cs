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

        public static void CopyFragTo(Bitmap dstBmp, Bitmap srcBmp, int xFrom, int yFrom)
        {
            using var dst = new SmartBmp(dstBmp, format: dstBmp.PixelFormat);
            using var src = new SmartBmp(srcBmp, format: srcBmp.PixelFormat);
            for (int x = 0; x < src.Width; x++)
                for (int y = 0; y < src.Height; y++)
                    dst[x + xFrom, y + yFrom] = src[x, y];
        }

        static void Log(string s)
        {
            Console.WriteLine(s);
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Restart the program with valid params");
                return;
            }

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
                        matrix[x, y] = Bytes2UShort(File.ReadAllBytes(path));
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
                interpolatedCount = InterpolateBrokenDots(frag, size);

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
                        var bytesFor32 = UShorts2ByteCompress(frag, maxByte + 1);
                        var bytesFor64 = frag.Select(c => (ushort)(c * 256 / (maxByte + 1))).ToArray();
                        var frag32 = Hgt2Png(bytesFor32, size, PixelFormat.Format32bppArgb, byte.MaxValue);
                        var frag64 = Hgt2Png(bytesFor64, size, PixelFormat.Format64bppArgb, ushort.MaxValue);
                        CopyFragTo(res32, frag32, (maxY - y) * size, (maxX - x) * size);
                        CopyFragTo(res64, frag64, (maxY - y) * size, (maxX - x) * size);
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
