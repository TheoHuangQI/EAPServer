namespace SecsLite.Model
{
	public class AlarmItem
	{
		public string Name { get; set; }

		public uint ALID { get; set; }

		public byte ALCD { get; set; }

		public string Text { get; set; }

		public bool Enabled { get; set; }

		public uint CEIDSet { get; set; }

		public uint CEIDClear { get; set; }

		public AlarmItem()
		{
		}

		public AlarmItem(uint alarmID, string alarmText, bool enable)
		{
			ALID = alarmID;
			Text = alarmText;
			Enabled = enable;
		}
	}
}
