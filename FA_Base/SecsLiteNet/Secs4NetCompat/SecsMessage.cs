namespace Secs4Net
{
	public sealed class SecsMessage : IDisposable
	{
		public SecsMessage(byte s, byte f, bool replyExpected = true)
		{
			if (s > 127)
			{
				throw new ArgumentOutOfRangeException(nameof(s));
			}

			S = s;
			F = f;
			ReplyExpected = replyExpected;
		}

		public byte S { get; }

		public byte F { get; }

		public bool ReplyExpected { get; internal set; }

		public string Description { get; set; }

		public string Name { get; set; }

		public Item SecsItem { get; set; }

		public void Dispose()
		{
		}

		public override string ToString()
		{
			return $"'S{S}F{F}' {(ReplyExpected ? "W" : string.Empty)} {Name ?? string.Empty}";
		}
	}
}
