namespace SecsLite.Model
{
	public class EventItem
	{
		public LinkedReportListObject LinkedReportList = new LinkedReportListObject();

		public uint CEID { get; set; }

		public string Name { get; set; }

		public bool Enabled { get; set; }

		public string Desc { get; set; }

		public EventItem()
		{
			CEID = 0u;
			Name = "";
			Enabled = false;
			Desc = "";
		}

		public EventItem(uint ceid, string name, bool enable, string desc)
		{
			CEID = ceid;
			Name = name;
			Enabled = enable;
			Desc = desc;
		}
	}
}
