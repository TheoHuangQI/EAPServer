namespace SecsLite.Model
{
	public class ECItem
	{
		public uint ECID { get; set; }

		public SecsFormat Format { get; set; }

		public string ECName { get; set; }

		public string Units { get; set; }

		public string MinLimit { get; set; }

		public string MaxLimit { get; set; }

		public string DefaultValue { get; set; }

		public string Desc { get; set; }

		public ECItem()
		{
		}

		public ECItem(uint vid, SecsFormat format, string name, string defaulfValue = "", string units = "", string min = "", string max = "")
		{
			ECID = vid;
			Format = format;
			ECName = name;
			Units = units;
			MinLimit = min;
			MaxLimit = max;
			DefaultValue = defaulfValue;
		}
	}
}
