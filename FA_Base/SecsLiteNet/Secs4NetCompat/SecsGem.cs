using Microsoft.Extensions.Options;

namespace Secs4Net
{
	public sealed class SecsGem
	{
		private readonly HsmsConnection _connection;

		public SecsGem(IOptions<SecsGemOptions> options, HsmsConnection connection, ISecsGemLogger logger)
		{
			_connection = connection;
			DeviceId = options.Value.DeviceId;
		}

		public ushort DeviceId { get; }

		public Task<SecsMessage> SendAsync(SecsMessage message, CancellationToken cancellationToken = default)
		{
			return _connection.Session.SendAsync(message, cancellationToken);
		}

		public IAsyncEnumerable<PrimaryMessageWrapper> GetPrimaryMessageAsync(CancellationToken cancellationToken = default)
		{
			return _connection.Session.ReadPrimaryMessagesAsync(cancellationToken);
		}
	}
}
