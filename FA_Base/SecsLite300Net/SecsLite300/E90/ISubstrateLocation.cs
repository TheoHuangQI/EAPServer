namespace SecsLite300.E90
{
	public interface ISubstrateLocation
	{
		string SubstID { get; }

		SubstLocState State { get; }

		bool DisableEvents { get; }
	}
}
