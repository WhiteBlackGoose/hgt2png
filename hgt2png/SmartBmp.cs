using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hgt2png
{
    unsafe struct FastBmp
    {
        private int* scan0;
        public int Width { get; }
        public int Height { get; }

        public FastBmp(BitmapData data)
        {
            scan0 = (int*)data.Scan0;
            Width = data.Width;
            Height = data.Height;
        }

        public Color this[int x, int y]
        {
            get => Color.FromArgb(*(scan0 + x + Width * y));
            set => *(scan0 + x + Width * y) = value.ToArgb();
        }

        public bool InBounds(int x, int y)
            => x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public class SmartBmp : IDisposable
    {
        private Bitmap bmp;
        private FastBmp fast;
        private BitmapData data;
        public SmartBmp(Bitmap bmp, ImageLockMode mode = ImageLockMode.ReadWrite, PixelFormat format = PixelFormat.Format32bppArgb)
        {
            this.bmp = bmp;
            data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), mode, format);
            fast = new FastBmp(data);
        }

        public Color this[int x, int y]
        {
            get => fast[x, y];
            set => fast[x, y] = value;
        }

        public int Width => fast.Width;
        public int Height => fast.Height;

        public void Dispose()
        {
            bmp.UnlockBits(data);
        }

        public bool InBounds(int x, int y) => fast.InBounds(x, y);
    }
}
