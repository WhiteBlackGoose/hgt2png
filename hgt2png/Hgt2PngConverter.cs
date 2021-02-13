using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hgt2png
{
    public static class Hgt2PngConverter
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
    }
}
