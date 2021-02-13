using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hgt2png
{
    public static class Convert
    {
        public static unsafe ushort[] Bytes2UShort(byte[] bytes)
        {
            var ushorts = new ushort[bytes.Length / 2];
            bytes = bytes.Reverse().ToArray();
            fixed (void* bytesPtr = bytes, ushortsPtr = ushorts)
                Buffer.MemoryCopy(bytesPtr, ushortsPtr, bytes.Length, bytes.Length);
            return ushorts.Reverse().ToArray();
        }

        public static byte[] UShorts2ByteCompress(ushort[] ushorts, int div)
            => ushorts.Select(c => (byte)(c / div)).ToArray();
    }
}
