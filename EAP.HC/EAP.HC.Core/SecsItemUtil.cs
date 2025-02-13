using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class SecsItemUtil
    {
        public static Object GetItemValue(Item item)
        {
            switch (item.Format)
            {
                case SecsFormat.List:
                    return null;
                case SecsFormat.ASCII:
                case SecsFormat.JIS8:
                    return item.GetString();
                case SecsFormat.Binary:
                    return MemoryMarshal.GetReference(item.GetMemory<byte>().Span);
                case SecsFormat.F4:

                    return MemoryMarshal.GetReference(item.GetMemory<float>().Span);
                case SecsFormat.F8:
                    return MemoryMarshal.GetReference(item.GetMemory<double>().Span);
                case SecsFormat.I1:
                    return MemoryMarshal.GetReference(item.GetMemory<sbyte>().Span);
                case SecsFormat.I2:
                    return MemoryMarshal.GetReference(item.GetMemory<short>().Span);
                case SecsFormat.I4:
                    return MemoryMarshal.GetReference(item.GetMemory<int>().Span);
                case SecsFormat.I8:
                    return MemoryMarshal.GetReference(item.GetMemory<long>().Span);
                case SecsFormat.U1:
                    return MemoryMarshal.GetReference(item.GetMemory<byte>().Span);
                case SecsFormat.U2:
                    return MemoryMarshal.GetReference(item.GetMemory<ushort>().Span);
                case SecsFormat.U4:
                    return MemoryMarshal.GetReference(item.GetMemory<uint>().Span);
                case SecsFormat.U8:
                    return MemoryMarshal.GetReference(item.GetMemory<ulong>().Span);
                case SecsFormat.Boolean:
                    return MemoryMarshal.GetReference(item.GetMemory<bool>().Span);
                default:
                    return null;
            }
        }
    }
}
