using System.Collections.Concurrent;
using System.Threading.Channels;
using SecsLite;
using SecsLite.Model;

namespace Secs4Net
{
	internal sealed class FaSecsSession : HsmsSession
	{
		private readonly HsmsConnection _connection;
		private readonly ISecsGemLogger _logger;
		private readonly Channel<PrimaryMessageWrapper> _primaryMessages = Channel.CreateUnbounded<PrimaryMessageWrapper>();
		private readonly ConcurrentDictionary<uint, TaskCompletionSource<SecsMessage>> _pendingReplies = new();

		public FaSecsSession(HsmsConnection connection, ISecsGemLogger logger)
		{
			_connection = connection;
			_logger = logger;
		}

		public IAsyncEnumerable<PrimaryMessageWrapper> ReadPrimaryMessagesAsync(CancellationToken cancellationToken)
		{
			return _primaryMessages.Reader.ReadAllAsync(cancellationToken);
		}

		public Task<SecsMessage> SendAsync(SecsMessage message, CancellationToken cancellationToken)
		{
			var item = message.SecsItem?.ToSecsLiteItem();
			var messageId = SendMessage(message.S, message.F, item, message.ReplyExpected);
			_logger?.MessageOut(message, unchecked((int)messageId));

			if (!message.ReplyExpected)
			{
				return Task.FromResult<SecsMessage>(null);
			}

			if (messageId == 0)
			{
				throw new SecsException($"Send S{message.S}F{message.F} failed.");
			}

			var completion = new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingReplies[messageId] = completion;

			if (cancellationToken.CanBeCanceled)
			{
				cancellationToken.Register(() =>
				{
					if (_pendingReplies.TryRemove(messageId, out var pending))
					{
						pending.TrySetCanceled(cancellationToken);
					}
				});
			}

			return completion.Task;
		}

		public Task<bool> ReplyAsync(uint messageId, SecsMessage message, CancellationToken cancellationToken)
		{
			message.ReplyExpected = false;
			var item = message.SecsItem?.ToSecsLiteItem();
			var result = ReplyMessage(message.S, message.F, item, messageId);
			_logger?.MessageOut(message, unchecked((int)messageId));
			return Task.FromResult(result != 0 || messageId != 0);
		}

		public override bool OnConnect(HsmsSessionInfo info)
		{
			var connected = base.OnConnect(info);
			_connection.OnConnected();
			return connected;
		}

		public override void OnDisconnect()
		{
			base.OnDisconnect();
			_connection.OnDisconnected();
		}

		public override SecsErrorCode OnReceiveMessage(byte stream, byte func, SecsItem item, uint messageId, bool IsEapSend = true)
		{
			base.OnReceiveMessage(stream, func, item, messageId, IsEapSend);
			var message = new SecsMessage(stream, func)
			{
				SecsItem = Item.FromSecsLiteItem(item),
			};
			_connection.OnMessageTraffic();

			_logger?.MessageIn(message, unchecked((int)messageId));

			if (_pendingReplies.TryRemove(messageId, out var pendingReply))
			{
				message.ReplyExpected = false;
				pendingReply.TrySetResult(message);
				return SecsErrorCode.Success;
			}

			_primaryMessages.Writer.TryWrite(new PrimaryMessageWrapper(this, message, messageId));
			return SecsErrorCode.Success;
		}

		public override void OnMessageReplyTimeout(uint messageId)
		{
			base.OnMessageReplyTimeout(messageId);
			if (_pendingReplies.TryRemove(messageId, out var pendingReply))
			{
				pendingReply.TrySetException(new TimeoutException($"SECS message 0x{messageId:X8} reply timeout."));
			}
		}
	}
}
