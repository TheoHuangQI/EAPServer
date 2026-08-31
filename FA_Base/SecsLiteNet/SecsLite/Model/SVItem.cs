namespace SecsLite.Model
{
	public class SVItem
	{
		public uint SVID { get; set; }

		public SecsFormat Format { get; set; }

		public string SVName { get; set; }

		public string Units { get; set; }

		public SVItem()
		{
		}

		public SVItem(uint vid, SecsFormat format, string name, string units = "")
		{
			SVID = vid;
			Format = format;
			SVName = name;
			Units = units;
		}
	}
}
