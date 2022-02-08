using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIRCUS_MES_FA
{
    static class Extensions
    {
        public static byte[] ReadCString(this BinaryReader reader)
        {
            var buffer = new List<byte>();

            for (byte value = reader.ReadByte(); value != 0; value = reader.ReadByte())
            {
                buffer.Add(value);
            }

            return buffer.ToArray();
        }
    }
}
