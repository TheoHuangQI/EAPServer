namespace Secs4Net
{
	public sealed class PrimaryMessageWrapper
	{
		private readonly FaSecsSession _session;
		private readonly uint _id;
		private readonly SemaphoreSlim _replyLock = new(1, 1);

		internal PrimaryMessageWrapper(FaSecsSession session, SecsMessage primaryMessage, uint id)
		{
			_session = session;
			PrimaryMessage = primaryMessage;
			Id = unchecked((int)id);
			_id = id;
		}

		public SecsMessage PrimaryMessage { get; }

		public int Id { get; }

		public SecsMessage SecondaryMessage { get; private set; }

		public async Task<bool> TryReplyAsync(SecsMessage replyMessage = null, CancellationToken cancellation = default)
		{
			await _replyLock.WaitAsync(cancellation).ConfigureAwait(false);
			try
			{
				if (SecondaryMessage != null)
				{
					return false;
				}

				replyMessage ??= new SecsMessage(9, 7, false);
				var result = await _session.ReplyAsync(_id, replyMessage, cancellation).ConfigureAwait(false);
				if (result)
				{
					SecondaryMessage = replyMessage;
				}

				return result;
			}
			finally
			{
				_replyLock.Release();
			}
		}

		public override string ToString()
		{
			return PrimaryMessage.ToString();
		}
	}
}
