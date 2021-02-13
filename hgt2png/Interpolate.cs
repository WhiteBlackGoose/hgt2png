using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hgt2png
{
    public static class Interpolate
    {
        private static bool BadDot(ushort dot)
            => dot / 256 == 128 || dot / 256 == 255;

        private static ushort CollectAround(ushort[] bytes, int currId, int size)
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
    }
}
