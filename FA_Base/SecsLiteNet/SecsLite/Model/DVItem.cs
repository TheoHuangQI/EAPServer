namespace SecsLite.Model
{
	public class DVItem
	{
		public uint VID { get; set; }

		public SecsFormat Format { get; set; }

		public string DVName { get; set; }

		public string Units { get; set; }

		public DVItem()
		{
		}

		public DVItem(uint vid, SecsFormat format, string name, string units = "")
		{
			VID = vid;
			Format = format;
			DVName = name;
			Units = units;
		}
	}
}
