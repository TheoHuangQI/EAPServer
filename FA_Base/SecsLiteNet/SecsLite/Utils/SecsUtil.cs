#define DEBUG
using SecsLite.Model;
using System;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace SecsLite
{
	public static class SecsUtil
	{
		public static SecsItem CreateBasicItemFromString(SecsFormat fmt, string valStr)
		{
			switch (fmt)
			{
			case SecsFormat.Ascii:
				return new SecsItem(valStr);
			case SecsFormat.Boolean:
				return new SecsItem(valStr.Trim() == "0" || valStr.Trim().ToLower() == "false");
			case SecsFormat.F4:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToSingle(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.F8:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToDouble(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.I1:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToChar(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.I2:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToInt16(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.I4:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToInt32(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.I8:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToInt64(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.U1:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToByte(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.U2:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToUInt16(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.U4:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToUInt32(valStr));
				}
				return new SecsItem(valStr);
			case SecsFormat.U8:
				if (!string.IsNullOrEmpty(valStr))
				{
					return new SecsItem(Convert.ToUInt64(valStr));
				}
				return new SecsItem(valStr);
			default:
				return null;
			}
		}

		public static void SecsMessageToSML(SecsItem item,StringBuilder text,int indentCount = 0)
		{
            try
            {
				if (item == null)
				{
					text.Append("<header only>");
					return;
				}
				if (item.Format == SecsFormat.List)
				{
					for (int i = 0; i < indentCount; i++)
					{
						text.Append("   ");
					}
					text.Append($"<L[{item.ItemList.Count}] ");
					foreach (SecsItem item2 in item.ItemList)
					{
						text.Append(Environment.NewLine);
						SecsMessageToSML(item2, text,indentCount + 1);
					}
					text.Append(Environment.NewLine);
					for (int j = 0; j < indentCount; j++)
					{
						text.Append("   ");
					}
					text.Append(">");
					return;
				}
				SimpleMessageToSML(item, text, indentCount);
			}
			catch(Exception ex)
            {
				return;
            }
		}
		public static SecsItem SecsMessageFromSML(string sml)
		{
			throw new NotImplementedException();
		}

		private static void SimpleMessageToSML(SecsItem item, StringBuilder text, int indentCount = 0)
		{
            try
            {
				for (int i = 0; i < indentCount; i++)
				{
					text.Append("   ");
				}
				text.Append("<");
				switch (item.Format)
				{
					case SecsFormat.Ascii:
						{
							string text2 = ((item.Value == null) ? null : (item.Value as string));
							int num2 = text2?.Length ?? 0;
							text.Append($"A[{num2}] {text2} >");
							break;
						}
					case SecsFormat.Binary:
					case SecsFormat.JIS8:
					case SecsFormat.DBytes:
						{
							byte[] array = ((item.Value == null) ? null : (item.Value as byte[]));
							int num = ((array != null) ? array.Length : 0);
							text.Append($"B[{num}] {BitConverter.ToString(array)} >");
							break;
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
							text.Append($"{item.Format.ToString()}[{item.ValueArray.Length}] ");
							for (int j = 0; j < item.ValueArray.Length; j++)
							{
								text.Append(item.ValueArray[j].ToString());
								text.Append(" ");
							}
							text.Append(" >");
							break;
						}
					default:
						break;
				}
				return;
			}
			catch(Exception ex)
            {
				return ;
            }
		}

		public static string XmlGetNodeAttrVal(XmlNode node, string attrName, string defaultVal = "")
		{
			XmlAttribute xmlAttribute = node.Attributes[attrName];
			return (xmlAttribute == null) ? defaultVal : xmlAttribute.InnerText;
		}

		public static void XmlSetNodeAttrVal(XmlDocument doc, XmlNode node, string attrName, string value)
		{
			XmlNode xmlNode = doc.CreateNode(XmlNodeType.Attribute, attrName, "");
			xmlNode.Value = value;
			node.Attributes.SetNamedItem(xmlNode);
		}

		public static string FormatTimeString(DateTime dt, TimeFormat fmt)
		{
			return fmt switch
			{
				TimeFormat.ShortFormat => dt.ToString("yyMMddHHmmss"), 
				TimeFormat.LongFormat => dt.ToString("yyyyMMddHHmmssff"), 
				_ => throw new NotImplementedException(), 
			};
		}

		public static SecsFormat StringToFormat(string fmtName)
		{
			switch (fmtName.ToLower())
			{
			case "list":
				return SecsFormat.List;
			case "b":
			case "bin":
			case "binary":
				return SecsFormat.Binary;
			case "bool":
			case "boolean":
				return SecsFormat.Boolean;
			case "a":
			case "ascii":
				return SecsFormat.Ascii;
			case "jis8":
				return SecsFormat.JIS8;
			case "db":
			case "dbyte":
			case "dbytes":
				return SecsFormat.DBytes;
			case "i1":
				return SecsFormat.I1;
			case "i2":
				return SecsFormat.I2;
			case "i4":
				return SecsFormat.I4;
			case "i8":
				return SecsFormat.I8;
			case "u1":
				return SecsFormat.U1;
			case "u2":
				return SecsFormat.U2;
			case "u4":
				return SecsFormat.U4;
			case "u8":
				return SecsFormat.U8;
			case "f4":
				return SecsFormat.F4;
			case "f8":
				return SecsFormat.F8;
			default:
				return SecsFormat.Unknown;
			}
		}
	}
}
