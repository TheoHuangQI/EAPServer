#define DEBUG


using FALogger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SecsLite.Model
{
	public class SecsItem
	{
		public object[] ValueArray = null;

		private List<SecsItem> m_list = null;

		public static string ClassName = "SecsItem";

		public bool IsArray { get; set; }

		public object Value
		{
			get
			{
				if (ValueArray == null)
				{
					ValueArray = new object[1];
				}
				if (ValueArray.Length == 0)
				{
					return null;
				}
				return ValueArray[0];
			}
			set
			{
				if (ValueArray == null)
				{
					ValueArray = new object[1];
				}
				ValueArray[0] = value;
			}
		}

		public SecsFormat Format { get; }

		public List<SecsItem> ItemList
		{
			get
			{
				return m_list;
			}
			set
			{
				m_list = value;
			}
		}

		public SecsItem(SecsFormat fmt)
		{
			Format = fmt;
			Value = null;
			if (fmt == SecsFormat.List)
			{
				ItemList = new List<SecsItem>();
			}
		}

		public SecsItem(SecsFormat fmt, int arraySize)
		{
			Format = fmt;
			if (arraySize > 1)
			{
				IsArray = true;
			}
			ValueArray = new object[arraySize];
		}

		public SecsItem(string v)
		{
			Value = v;
			Format = SecsFormat.Ascii;
		}

		public SecsItem(bool v)
		{
			Value = v;
			Format = SecsFormat.Boolean;
		}

		public SecsItem(char v)
		{
			Value = v;
			Format = SecsFormat.I1;
		}

		public SecsItem(sbyte v)
		{
			Value = v;
			Format = SecsFormat.I1;
		}

		public SecsItem(short v)
		{
			Value = v;
			Format = SecsFormat.I2;
		}

		public SecsItem(int v)
		{
			Value = v;
			Format = SecsFormat.I4;
		}

		public SecsItem(long v)
		{
			Value = v;
			Format = SecsFormat.I8;
		}

		public SecsItem(byte v)
		{
			Value = v;
			Format = SecsFormat.U1;
		}

		public SecsItem(ushort v)
		{
			Value = v;
			Format = SecsFormat.U2;
		}

		public SecsItem(uint v)
		{
			Value = v;
			Format = SecsFormat.U4;
		}

		public SecsItem(ulong v)
		{
			Value = v;
			Format = SecsFormat.U8;
		}

		public SecsItem(float v)
		{
			Value = v;
			Format = SecsFormat.F4;
		}

		public SecsItem(double v)
		{
			Value = v;
			Format = SecsFormat.F8;
		}

		public SecsItem(SecsFormat format, byte[] v)
		{
			if (format == SecsFormat.Binary || format == SecsFormat.JIS8 || format == SecsFormat.DBytes)
			{
				Value = v;
				Format = format;
				return;
			}
			FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Only binary, jis8 and dbytes format can be initialized from a byte array.");
			throw new Exception("Only binary, jis8 and dbytes format can be initialized from a byte array. ");
		}

		public static SecsItem CreateBinaryItem(byte v)
		{
			byte[] v2 = new byte[1] { v };
			return new SecsItem(SecsFormat.Binary, v2);
		}

		public byte GetSingleBinaryValue()
		{
			if (Format != SecsFormat.Binary)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: item format must be binary.");
				throw new Exception("item format must be binary.");
			}
			byte[] array = Value as byte[];
			if (array.Length != 1)
			{
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: the length of item binary data array exceeds.");
				throw new Exception("the length of item binary data array exceeds.");
			}
			return array[0];
		}

		public bool IsUnsignedInterger()
		{
			return Format == SecsFormat.U1 || Format == SecsFormat.U2 || Format == SecsFormat.U4 || Format == SecsFormat.U8;
		}

		public static SecsFormat GetObjectFormat(object obj)
		{
			Type type = obj.GetType();
			if (type == typeof(char))
			{
				return SecsFormat.I1;
			}
			if (type == typeof(short))
			{
				return SecsFormat.I2;
			}
			if (type == typeof(int))
			{
				return SecsFormat.I4;
			}
			if (type == typeof(long))
			{
				return SecsFormat.I8;
			}
			if (type == typeof(byte))
			{
				return SecsFormat.U1;
			}
			if (type == typeof(ushort))
			{
				return SecsFormat.U2;
			}
			if (type == typeof(uint))
			{
				return SecsFormat.U4;
			}
			if (type == typeof(ulong))
			{
				return SecsFormat.U8;
			}
			if (type == typeof(float))
			{
				return SecsFormat.F4;
			}
			if (type == typeof(double))
			{
				return SecsFormat.F8;
			}
			if (type == typeof(string))
			{
				return SecsFormat.Ascii;
			}
			if (type == typeof(byte[]))
			{
				return SecsFormat.Binary;
			}
			return SecsFormat.Unknown;
		}

		public static SecsItem CreateItem(object val)
		{
			return CreateItem(val, GetObjectFormat(val));
		}

		public static SecsItem CreateItem(object val, SecsFormat fmt)
		{
            if (val == null)
            {
				return new SecsItem(fmt);
            }
			switch (fmt)
			{
			case SecsFormat.Binary:
			{
				byte[] v = (byte[])val;
				return new SecsItem(SecsFormat.Binary, v);
			}
			case SecsFormat.Ascii:
				return new SecsItem(val as string);
			case SecsFormat.Boolean:
				return new SecsItem((bool)val);
			case SecsFormat.I1:
				return new SecsItem((char)val);
			case SecsFormat.I2:
				return new SecsItem((short)val);
			case SecsFormat.I4:
				return new SecsItem((int)val);
			case SecsFormat.I8:
				return new SecsItem((long)val);
			case SecsFormat.U1:
				return new SecsItem((byte)val);
			case SecsFormat.U2:
				return new SecsItem((ushort)val);
			case SecsFormat.U4:
				return new SecsItem((uint)val);
			case SecsFormat.U8:
				return new SecsItem((ulong)val);
			case SecsFormat.F4:
				return new SecsItem((float)val);
			case SecsFormat.F8:
				return new SecsItem((double)val);
			default:
				throw new NotImplementedException();
			}
		}

		public bool ToBoolean()
		{
			switch (Format)
			{
			case SecsFormat.Binary:
			{
				byte[] array = (byte[])Value;
				if (array.Length != 1)
				{
					throw new Exception("Can't convert byte array to bool");
				}
				return array[0] != 0;
			}
			case SecsFormat.Ascii:
				return ((string)Value).Trim() != "0";
			case SecsFormat.Boolean:
			case SecsFormat.I8:
			case SecsFormat.I1:
			case SecsFormat.I2:
			case SecsFormat.I4:
			case SecsFormat.F8:
			case SecsFormat.F4:
			case SecsFormat.U8:
			case SecsFormat.U1:
			case SecsFormat.U2:
			case SecsFormat.U4:
				return Convert.ToBoolean(Value);
			default:
				throw new Exception("Can't convert " + Format.ToString() + " to bool");
			}
		}

		public void AppendItem(SecsItem item)
		{
			if (Format != 0)
			{
				throw new Exception("Can't append subitem to non-list format item.");
			}
			ItemList.Add(item);
		}

		public static int GetBasicFormatSize(SecsFormat format)
		{
			switch (format)
			{
			case SecsFormat.List:
			case SecsFormat.Binary:
			case SecsFormat.Ascii:
			case SecsFormat.JIS8:
			case SecsFormat.DBytes:
				return 0;
			case SecsFormat.Boolean:
				return 1;
			case SecsFormat.F4:
				return 4;
			case SecsFormat.F8:
				return 8;
			case SecsFormat.I1:
				return 1;
			case SecsFormat.I2:
				return 2;
			case SecsFormat.I4:
				return 4;
			case SecsFormat.I8:
				return 8;
			case SecsFormat.U1:
				return 1;
			case SecsFormat.U2:
				return 2;
			case SecsFormat.U4:
				return 4;
			case SecsFormat.U8:
				return 8;
			default:
				Debug.Assert(condition: false);
				return 0;
			}
		}

		public int GetItemDataSize()
		{
			int num = 2;
			switch (Format)
			{
			case SecsFormat.List:
			{
				if (ItemList.Count > 255)
				{
					num++;
				}
				if (ItemList.Count > 65535)
				{
					num++;
				}
				int num2 = num;
				for (int i = 0; i < ItemList.Count; i++)
				{
					if (ItemList[i] != null)
					{
						num2 += ItemList[i].GetItemDataSize();
					}
				}
				return num2;
			}
			case SecsFormat.Ascii:
			{
				if (Value == null)
				{
					Value = "";
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Ascii is null");

				}
				string text = (string)Value;
				if (text.Length > 255)
				{
					num++;
				}
				if (text.Length > 65535)
				{
					num++;
				}
				return num + text.Length;
			}
			case SecsFormat.Binary:
			case SecsFormat.JIS8:
			case SecsFormat.DBytes:
			{
				if (Value == null)
				{
					FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: DBytes is null");
					return num;
				}
				byte[] array = (byte[])Value;
				if (array.Length > 255)
				{
					num++;
				}
				if (array.Length > 65535)
				{
					num++;
				}
				return num + array.Length;
			}
			case SecsFormat.Boolean:
			case SecsFormat.I8:
			case SecsFormat.I1:
			case SecsFormat.I2:
			case SecsFormat.I4:
			case SecsFormat.F8:
			case SecsFormat.F4:
			case SecsFormat.U8:
			case SecsFormat.U1:
			case SecsFormat.U2:
			case SecsFormat.U4:
			{
				if (ValueArray.Length > 255)
				{
					num++;
				}
				if (ValueArray.Length > 65535)
				{
					num++;
				}
				int basicFormatSize = GetBasicFormatSize(Format);
				return num + basicFormatSize * ValueArray.Length;
			}
			default:
				throw new InvalidDataException();
			}
		}

		public static int FillToBuffer(IntPtr dataPtr, SecsItem item)
		{
			if (item == null)
			{
				return 0;
			}
			IntPtr intPtr = dataPtr;
			byte b = (byte)item.Format;
			byte b2 = 1;
			int num = 0;
			num = ((item.Format == SecsFormat.List) ? item.ItemList.Count : ((item.Format == SecsFormat.Ascii) ? ((string)item.Value).Length : (((item.Format != SecsFormat.JIS8 && item.Format != SecsFormat.Binary && item.Format != SecsFormat.DBytes) || item.Value == null) ? (GetBasicFormatSize(item.Format) * item.ValueArray.Length) : ((byte[])item.Value).Length)));
			if (num > 255)
			{
				b2 = (byte)(b2 + 1);
			}
			if (num > 65535)
			{
				b2 = (byte)(b2 + 1);
			}
			b = (byte)(b | b2);
			Marshal.WriteByte(dataPtr, b);
			dataPtr += 1;
			if (b2 > 2)
			{
				byte val = (byte)((uint)(num >> 16) & 0xFFu);
				Marshal.WriteByte(dataPtr, val);
				dataPtr += 1;
			}
			if (b2 > 1)
			{
				byte val2 = (byte)((uint)(num >> 8) & 0xFFu);
				Marshal.WriteByte(dataPtr, val2);
				dataPtr += 1;
			}
			Marshal.WriteByte(dataPtr, (byte)num);
			dataPtr += 1;
			switch (item.Format)
			{
			case SecsFormat.List:
			{
				for (int m = 0; m < item.ItemList.Count; m++)
				{
					int num2 = FillToBuffer(dataPtr, item.ItemList[m]);
					dataPtr += num2;
				}
				break;
			}
			case SecsFormat.Ascii:
			{
				byte[] bytes = Encoding.ASCII.GetBytes((string)item.Value);
				Marshal.Copy(bytes, 0, dataPtr, bytes.Length);
				dataPtr += bytes.Length;
				break;
			}
			case SecsFormat.Binary:
			case SecsFormat.JIS8:
			case SecsFormat.DBytes:
			{
				byte[] array5 = (byte[])item.Value;
				Marshal.Copy(array5, 0, dataPtr, array5.Length);
				dataPtr += array5.Length;
				break;
			}
			case SecsFormat.I1:
			{
				for (int k = 0; k < item.ValueArray.Length; k++)
				{
					sbyte tmp = (sbyte)item.ValueArray[k];
					Marshal.WriteByte(dataPtr, (byte)tmp);
					dataPtr += 1;
				}
				break;
			}
			case SecsFormat.Boolean:
			case SecsFormat.U1:
			{
				for (int k = 0; k < item.ValueArray.Length; k++)
				{
					Marshal.WriteByte(dataPtr, Convert.ToByte(item.ValueArray[k]));
					dataPtr += 1;
				}
				break;
			}
			case SecsFormat.F4:
			{
				for (int num3 = 0; num3 < item.ValueArray.Length; num3++)
				{
					float value5 = (float)item.ValueArray[num3];
					byte[] array6 = BitConverter.GetBytes(value5).Reverse().ToArray();
					Marshal.Copy(array6, 0, dataPtr, array6.Length);
					dataPtr += array6.Length;
				}
				break;
			}
			case SecsFormat.F8:
			{
				for (int n = 0; n < item.ValueArray.Length; n++)
				{
					double value4 = (double)item.ValueArray[n];
					byte[] array4 = BitConverter.GetBytes(value4).Reverse().ToArray();
					Marshal.Copy(array4, 0, dataPtr, array4.Length);
					dataPtr += array4.Length;
				}
				break;
			}
			case SecsFormat.I2:
			case SecsFormat.U2:
			{
				for (int l = 0; l < item.ValueArray.Length; l++)
				{
					short value3 = Convert.ToInt16(item.ValueArray[l]);
					byte[] array3 = BitConverter.GetBytes(value3).Reverse().ToArray();
					Marshal.Copy(array3, 0, dataPtr, array3.Length);
					dataPtr += array3.Length;
				}
				break;
			}
			case SecsFormat.I4:
			case SecsFormat.U4:
			{
				for (int j = 0; j < item.ValueArray.Length; j++)
				{
					int value2 = Convert.ToInt32(item.ValueArray[j]);
					byte[] array2 = BitConverter.GetBytes(value2).Reverse().ToArray();
					Marshal.Copy(array2, 0, dataPtr, array2.Length);
					dataPtr += array2.Length;
				}
				break;
			}
			case SecsFormat.I8:
			case SecsFormat.U8:
			{
				for (int i = 0; i < item.ValueArray.Length; i++)
				{
					long value = Convert.ToInt64(item.ValueArray[i]);
					byte[] array = BitConverter.GetBytes(value).Reverse().ToArray();
					Marshal.Copy(array, 0, dataPtr, array.Length);
					dataPtr += array.Length;
				}
				break;
			}
			default:
				throw new InvalidDataException();
			}
			return (int)(dataPtr.ToInt64() - intPtr.ToInt64());
		}

		public static int LoadFromBuffer(IntPtr dataPtr, uint dataLen, ref SecsItem item)
		{
			Debug.Assert(item == null);
			if (dataLen == 0)
			{
				return 0;
			}
			IntPtr intPtr = dataPtr;
			byte b = Marshal.ReadByte(intPtr, 0);
			byte b2 = (byte)(b & 0xFCu);
			if (!Enum.IsDefined(typeof(SecsFormat), (int)b2))
			{
				throw new Exception("unknown SECS data type");
			}
			SecsFormat secsFormat = (SecsFormat)b2;
			byte b3 = (byte)(b & 3u);
			intPtr += 1;
			int num = Marshal.ReadByte(intPtr);
			if (b3 > 1)
			{
				num = Marshal.ReadByte(intPtr + 1) + num * 256;
			}
			if (b3 > 2)
			{
				num = Marshal.ReadByte(intPtr + 2) + num * 256;
			}
			intPtr += (int)b3;
			int num2 = 0;
			if (secsFormat == SecsFormat.List)
			{
				item = new SecsItem(SecsFormat.List);
			}
			else
			{
				int basicFormatSize = GetBasicFormatSize(secsFormat);
				num2 = ((basicFormatSize == 0) ? 1 : (num / basicFormatSize));
				item = new SecsItem(secsFormat, num2);
			}
			switch (item.Format)
			{
			case SecsFormat.Boolean:
			{
				for (int num5 = 0; num5 < num2; num5++)
				{
					item.ValueArray[num5] = Convert.ToBoolean(Marshal.ReadByte(intPtr));
					intPtr += 1;
				}
				break;
			}
			case SecsFormat.I1:
			{
				for (int num9 = 0; num9 < num2; num9++)
				{
					item.ValueArray[num9] = Convert.ToChar(Marshal.ReadByte(intPtr));
					intPtr += 1;
				}
				break;
			}
			case SecsFormat.I2:
			{
				for (int l = 0; l < num2; l++)
				{
					short value = Marshal.ReadInt16(intPtr);
					item.ValueArray[l] = BitConverter.ToInt16(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
					intPtr += 2;
				}
				break;
			}
			case SecsFormat.I4:
			{
				for (int num7 = 0; num7 < num2; num7++)
				{
					int value3 = Marshal.ReadInt32(intPtr);
					item.ValueArray[num7] = BitConverter.ToInt32(BitConverter.GetBytes(value3).Reverse().ToArray(), 0);
					intPtr += 4;
				}
				break;
			}
			case SecsFormat.I8:
			{
				for (int n = 0; n < num2; n++)
				{
					long value2 = Marshal.ReadInt64(intPtr);
					item.ValueArray[n] = BitConverter.ToInt64(BitConverter.GetBytes(value2).Reverse().ToArray(), 0);
					intPtr += 8;
				}
				break;
			}
			case SecsFormat.U1:
			{
				for (int j = 0; j < num2; j++)
				{
					item.ValueArray[j] = Marshal.ReadByte(intPtr);
					intPtr += 1;
				}
				break;
			}
			case SecsFormat.U2:
			{
				for (int num8 = 0; num8 < num2; num8++)
				{
					byte[] array6 = new byte[2];
					Marshal.Copy(intPtr, array6, 0, 2);
					item.ValueArray[num8] = BitConverter.ToUInt16(array6.Reverse().ToArray(), 0);
					intPtr += 2;
				}
				break;
			}
			case SecsFormat.U4:
			{
				for (int num6 = 0; num6 < num2; num6++)
				{
					byte[] array5 = new byte[4];
					Marshal.Copy(intPtr, array5, 0, 4);
					item.ValueArray[num6] = BitConverter.ToUInt32(array5.Reverse().ToArray(), 0);
					intPtr += 4;
				}
				break;
			}
			case SecsFormat.U8:
			{
				for (int num4 = 0; num4 < num2; num4++)
				{
					byte[] array4 = new byte[8];
					Marshal.Copy(intPtr, array4, 0, 8);
					item.ValueArray[num4] = BitConverter.ToUInt64(array4.Reverse().ToArray(), 0);
					intPtr += 8;
				}
				break;
			}
			case SecsFormat.F4:
			{
				for (int m = 0; m < num2; m++)
				{
					float[] array3 = new float[1];
					Marshal.Copy(intPtr, array3, 0, 1);
					item.ValueArray[m] = BitConverter.ToSingle(BitConverter.GetBytes(array3[0]).Reverse().ToArray(), 0);
					intPtr += 4;
				}
				break;
			}
			case SecsFormat.F8:
			{
				for (int k = 0; k < num2; k++)
				{
					double[] array2 = new double[1];
					Marshal.Copy(intPtr, array2, 0, 1);
					item.ValueArray[k] = BitConverter.ToDouble(BitConverter.GetBytes(array2[0]).Reverse().ToArray(), 0);
					intPtr += 8;
				}
				break;
			}
			case SecsFormat.Binary:
			case SecsFormat.JIS8:
			case SecsFormat.DBytes:
			{
				byte[] array = new byte[num];
				Marshal.Copy(intPtr, array, 0, num);
				item.Value = array;
				intPtr += num;
				break;
			}
			case SecsFormat.Ascii:
				item.Value = Marshal.PtrToStringAnsi(intPtr, num);
				intPtr += num;
				break;
			case SecsFormat.List:
			{
				item.ItemList = new List<SecsItem>();
				for (int i = 0; i < num; i++)
				{
					SecsItem item2 = null;
					int num3 = LoadFromBuffer(intPtr, (uint)(dataLen - (intPtr.ToInt64() - dataPtr.ToInt64())), ref item2);
					item.ItemList.Add(item2);
					intPtr += num3;
				}
				break;
			}
			default:
				throw new Exception("unknown SECS data type. [2]");
			}
			return (int)(intPtr.ToInt64() - dataPtr.ToInt64());
		}
	}
}
