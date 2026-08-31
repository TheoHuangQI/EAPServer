using System.Collections.Generic;

namespace SecsLite.Model
{
	public class DataReport : List<uint>
	{
		public object LockObject = new object();

		public uint ID { get; set; }

		public DataReport(uint id)
		{
			ID = id;
		}
	}
}
