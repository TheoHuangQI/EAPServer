namespace SecsLite300.E87
{
	public class Location : ILocation
	{
		private string m_locID = "";

		private int m_locIndex = 0;

		public string CarrierID { get; set; }

		public string PartitionID { get; set; }

		public string LocID => m_locID;

		public int GlobalLocIndex => m_locIndex;

		public Location(byte ptn, LoadPortLocationType type)
		{
			switch (type)
			{
			case LoadPortLocationType.LoadPort:
				m_locID = $"LP{ptn}";
				m_locIndex = (ptn - 1) * 2 + 1;
				break;
			case LoadPortLocationType.FIMS:
				m_locID = $"FIMS{ptn}";
				m_locIndex = (ptn - 1) * 2 + 2;
				break;
			}
		}
	}
}
