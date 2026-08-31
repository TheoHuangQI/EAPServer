namespace FSMLite
{
	internal class TransitionItem
	{
		public object CurrentState { get; set; }

		public object NewState { get; set; }

		public object TriggerEvent { get; set; }

		public FSMAction Action { get; set; }

		public static uint MakeKey(ushort a, ushort b)
		{
			return (uint)((a << 16) + b);
		}
	}
}
