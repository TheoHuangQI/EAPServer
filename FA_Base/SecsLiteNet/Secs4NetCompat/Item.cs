using System.Runtime.InteropServices;
using SecsLite.Model;

namespace Secs4Net
{
	public sealed class Item : IDisposable
	{
		private Item(SecsFormat format, object value)
		{
			Format = format;
			_value = value;
		}

		private readonly object _value;

		public SecsFormat Format { get; }

		public int Count
		{
			get
			{
				if (Format == SecsFormat.List)
				{
					return Items.Length;
				}

				if (_value is string text)
				{
					return text.Length;
				}

				if (_value is Array array)
				{
					return array.Length;
				}

				return _value == null ? 0 : 1;
			}
		}

		public Item[] Items
		{
			get
			{
				if (Format != SecsFormat.List)
				{
					throw new NotSupportedException($"Items is not supported for {Format}.");
				}

				return (Item[])_value;
			}
		}

		public Item this[int index]
		{
			get => Items[index];
			set => Items[index] = value;
		}

		public string GetString()
		{
			if (Format != SecsFormat.ASCII && Format != SecsFormat.JIS8)
			{
				throw new NotSupportedException($"GetString is not supported for {Format}.");
			}

			return (string)_value;
		}

		public Memory<T> GetMemory<T>() where T : unmanaged, IEquatable<T>
		{
			if (_value is T[] values)
			{
				return values.AsMemory();
			}

			if (_value is byte[] bytes && typeof(T) == typeof(byte))
			{
				return MemoryMarshal.Cast<byte, T>(bytes.AsMemory().Span).ToArray().AsMemory();
			}

			throw new NotSupportedException($"GetMemory<{typeof(T).Name}> is not supported for {Format}.");
		}

		public object GetValue()
		{
			return _value;
		}

		public void Dispose()
		{
		}

		public static Item L(IEnumerable<Item> items) => L(items.ToArray());

		public static Item L(params Item[] items) => new Item(SecsFormat.List, items ?? Array.Empty<Item>());

		public static Item A(string value = "") => new Item(SecsFormat.ASCII, value ?? string.Empty);

		public static Item J(string value = "") => new Item(SecsFormat.JIS8, value ?? string.Empty);

		public static Item B(params byte[] value) => new Item(SecsFormat.Binary, value ?? Array.Empty<byte>());

		public static Item B(IEnumerable<byte> value) => B(value?.ToArray() ?? Array.Empty<byte>());

		public static Item U1(params byte[] value) => new Item(SecsFormat.U1, value ?? Array.Empty<byte>());

		public static Item U1(IEnumerable<byte> value) => U1(value?.ToArray() ?? Array.Empty<byte>());

		public static Item U2(params ushort[] value) => new Item(SecsFormat.U2, value ?? Array.Empty<ushort>());

		public static Item U2(IEnumerable<ushort> value) => U2(value?.ToArray() ?? Array.Empty<ushort>());

		public static Item U4(params uint[] value) => new Item(SecsFormat.U4, value ?? Array.Empty<uint>());

		public static Item U4(IEnumerable<uint> value) => U4(value?.ToArray() ?? Array.Empty<uint>());

		public static Item U8(params ulong[] value) => new Item(SecsFormat.U8, value ?? Array.Empty<ulong>());

		public static Item U8(IEnumerable<ulong> value) => U8(value?.ToArray() ?? Array.Empty<ulong>());

		public static Item I1(params sbyte[] value) => new Item(SecsFormat.I1, value ?? Array.Empty<sbyte>());

		public static Item I1(IEnumerable<sbyte> value) => I1(value?.ToArray() ?? Array.Empty<sbyte>());

		public static Item I2(params short[] value) => new Item(SecsFormat.I2, value ?? Array.Empty<short>());

		public static Item I2(IEnumerable<short> value) => I2(value?.ToArray() ?? Array.Empty<short>());

		public static Item I4(params int[] value) => new Item(SecsFormat.I4, value ?? Array.Empty<int>());

		public static Item I4(IEnumerable<int> value) => I4(value?.ToArray() ?? Array.Empty<int>());

		public static Item I8(params long[] value) => new Item(SecsFormat.I8, value ?? Array.Empty<long>());

		public static Item I8(IEnumerable<long> value) => I8(value?.ToArray() ?? Array.Empty<long>());

		public static Item F4(params float[] value) => new Item(SecsFormat.F4, value ?? Array.Empty<float>());

		public static Item F4(IEnumerable<float> value) => F4(value?.ToArray() ?? Array.Empty<float>());

		public static Item F8(params double[] value) => new Item(SecsFormat.F8, value ?? Array.Empty<double>());

		public static Item F8(IEnumerable<double> value) => F8(value?.ToArray() ?? Array.Empty<double>());

		public static Item Boolean(params bool[] value) => new Item(SecsFormat.Boolean, value ?? Array.Empty<bool>());

		public static Item Boolean(IEnumerable<bool> value) => Boolean(value?.ToArray() ?? Array.Empty<bool>());

		internal SecsItem ToSecsLiteItem()
		{
			switch (Format)
			{
				case SecsFormat.List:
					var list = new SecsItem(SecsLite.SecsFormat.List);
					foreach (var item in Items)
					{
						list.AppendItem(item?.ToSecsLiteItem());
					}
					return list;
				case SecsFormat.ASCII:
					return new SecsItem(GetString());
				case SecsFormat.JIS8:
					return new SecsItem(SecsLite.SecsFormat.JIS8, System.Text.Encoding.UTF8.GetBytes(GetString()));
				case SecsFormat.Binary:
					return new SecsItem(SecsLite.SecsFormat.Binary, (byte[])_value);
				case SecsFormat.Boolean:
					return CreateArrayItem(SecsLite.SecsFormat.Boolean, (bool[])_value);
				case SecsFormat.I1:
					return CreateArrayItem(SecsLite.SecsFormat.I1, (sbyte[])_value);
				case SecsFormat.I2:
					return CreateArrayItem(SecsLite.SecsFormat.I2, (short[])_value);
				case SecsFormat.I4:
					return CreateArrayItem(SecsLite.SecsFormat.I4, (int[])_value);
				case SecsFormat.I8:
					return CreateArrayItem(SecsLite.SecsFormat.I8, (long[])_value);
				case SecsFormat.U1:
					return CreateArrayItem(SecsLite.SecsFormat.U1, (byte[])_value);
				case SecsFormat.U2:
					return CreateArrayItem(SecsLite.SecsFormat.U2, (ushort[])_value);
				case SecsFormat.U4:
					return CreateArrayItem(SecsLite.SecsFormat.U4, (uint[])_value);
				case SecsFormat.U8:
					return CreateArrayItem(SecsLite.SecsFormat.U8, (ulong[])_value);
				case SecsFormat.F4:
					return CreateArrayItem(SecsLite.SecsFormat.F4, (float[])_value);
				case SecsFormat.F8:
					return CreateArrayItem(SecsLite.SecsFormat.F8, (double[])_value);
				default:
					throw new NotSupportedException($"Unsupported SECS item format {Format}.");
			}
		}

		internal static Item FromSecsLiteItem(SecsItem item)
		{
			if (item == null)
			{
				return null;
			}

			switch (item.Format)
			{
				case SecsLite.SecsFormat.List:
					return L(item.ItemList.Select(FromSecsLiteItem).ToArray());
				case SecsLite.SecsFormat.Ascii:
					return A(item.Value?.ToString() ?? string.Empty);
				case SecsLite.SecsFormat.JIS8:
					return J(item.Value is byte[] bytes ? System.Text.Encoding.UTF8.GetString(bytes) : item.Value?.ToString() ?? string.Empty);
				case SecsLite.SecsFormat.Binary:
					return B((byte[])item.Value);
				case SecsLite.SecsFormat.Boolean:
					return Boolean(item.ValueArray.Cast<bool>());
				case SecsLite.SecsFormat.I1:
					return I1(item.ValueArray.Select(Convert.ToSByte));
				case SecsLite.SecsFormat.I2:
					return I2(item.ValueArray.Select(Convert.ToInt16));
				case SecsLite.SecsFormat.I4:
					return I4(item.ValueArray.Select(Convert.ToInt32));
				case SecsLite.SecsFormat.I8:
					return I8(item.ValueArray.Select(Convert.ToInt64));
				case SecsLite.SecsFormat.U1:
					return U1(item.ValueArray.Select(Convert.ToByte));
				case SecsLite.SecsFormat.U2:
					return U2(item.ValueArray.Select(Convert.ToUInt16));
				case SecsLite.SecsFormat.U4:
					return U4(item.ValueArray.Select(Convert.ToUInt32));
				case SecsLite.SecsFormat.U8:
					return U8(item.ValueArray.Select(Convert.ToUInt64));
				case SecsLite.SecsFormat.F4:
					return F4(item.ValueArray.Select(Convert.ToSingle));
				case SecsLite.SecsFormat.F8:
					return F8(item.ValueArray.Select(Convert.ToDouble));
				default:
					throw new NotSupportedException($"Unsupported SECS item format {item.Format}.");
			}
		}

		private static SecsItem CreateArrayItem<T>(SecsLite.SecsFormat format, T[] values)
		{
			var item = new SecsItem(format, values?.Length ?? 0);
			for (var i = 0; i < item.ValueArray.Length; i++)
			{
				item.ValueArray[i] = values[i];
			}

			return item;
		}
	}
}
