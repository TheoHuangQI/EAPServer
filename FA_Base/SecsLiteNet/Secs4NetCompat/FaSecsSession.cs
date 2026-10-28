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
		private readonly object _pendingReplyLock = new();
		private long _disconnectVersion;
		private int _sendsInProgress;
		private readonly Dictionary<uint, SecsMessage> _earlyReplies = new();
		private const int MaxEarlyReplies = 1024;

		private void LogWarningSafely(string message)
		{
			try
			{
				_logger?.Warning(message);
			}
			catch
			{
				// Logging must not break SECS message processing.
			}
		}

		private void LogErrorSafely(string message, SecsMessage secsMessage, Exception exception)
		{
			try
			{
				_logger?.Error(message, secsMessage, exception);
			}
			catch
			{
				// Logging must not break SECS message processing.
			}
		}

		private void LogMessageOutSafely(SecsMessage message, int messageId)
		{
			try
			{
				_logger?.MessageOut(message, messageId);
			}
			catch
			{
				// Logging must not break SECS message processing.
			}
		}

		private void LogMessageInSafely(SecsMessage message, int messageId)
		{
			try
			{
				_logger?.MessageIn(message, messageId);
			}
			catch
			{
				// Logging must not break SECS message processing.
			}
		}

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
			return SendWithReplyTracking(message, cancellationToken, () =>
			{
				var item = message.SecsItem?.ToSecsLiteItem();
				return SendMessage(message.S, message.F, item, message.ReplyExpected);
			});
		}

		private Task<SecsMessage> SendWithReplyTracking(SecsMessage message,
			CancellationToken cancellationToken, Func<uint> send)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Task.FromCanceled<SecsMessage>(cancellationToken);
			}

			if (message == null)
			{
				LogWarningSafely("SECS send skipped because the message is null.");
				return Task.FromResult<SecsMessage>(null);
			}

			long disconnectVersion;
			lock (_pendingReplyLock)
			{
				disconnectVersion = _disconnectVersion;
				_sendsInProgress++;
			}

			try
			{
				// Never hold the reply lock while calling native code: it may invoke callbacks.
				var messageId = send();
				LogMessageOutSafely(message, unchecked((int)messageId));

				if (!message.ReplyExpected)
				{
					return Task.FromResult<SecsMessage>(null);
				}

				// SendMessage returns 0 when the HSMS session is not connected or the
				// native send fails.  A failed send is a normal runtime result here;
				// returning null lets callers handle it without an unhandled exception
				// escaping from an async event callback.
				if (messageId == 0 || messageId == uint.MaxValue)
				{
					// The native API returns -1 for send failures; P/Invoke exposes it as uint.MaxValue.
					LogWarningSafely($"Send S{message.S}F{message.F} failed: message id is 0x{messageId:X8}.");
					return Task.FromResult<SecsMessage>(null);
				}

				var completion = new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
				lock (_pendingReplyLock)
				{
					// A disconnect can arrive while the native send is still returning.
					if (disconnectVersion != _disconnectVersion)
					{
						return Task.FromResult<SecsMessage>(null);
					}
					if (_earlyReplies.Remove(messageId, out var earlyReply))
					{
						completion.TrySetResult(earlyReply);
					}
					else if (!_pendingReplies.TryAdd(messageId, completion))
					{
						throw new InvalidOperationException($"Duplicate pending SECS message id 0x{messageId:X8}.");
					}
				}

				return WaitForReplyAsync(messageId, completion, cancellationToken);
			}
			catch (Exception ex)
			{
				LogErrorSafely("SECS send failed.", message, ex);
				return Task.FromResult<SecsMessage>(null);
			}
			finally
			{
				lock (_pendingReplyLock)
				{
					_sendsInProgress--;
					if (_sendsInProgress == 0)
					{
						// Never retain unmatched/late replies after the native registration window.
						_earlyReplies.Clear();
					}
				}
			}
		}

		private async Task<SecsMessage> WaitForReplyAsync(uint messageId,
			TaskCompletionSource<SecsMessage> completion, CancellationToken cancellationToken)
		{
			// Dispose the registration when the reply, timeout or disconnect completes the task.
			using var registration = cancellationToken.Register(() =>
			{
				var entry = new KeyValuePair<uint, TaskCompletionSource<SecsMessage>>(messageId, completion);
				if (((ICollection<KeyValuePair<uint, TaskCompletionSource<SecsMessage>>>)_pendingReplies).Remove(entry))
				{
					completion.TrySetCanceled(cancellationToken);
				}
			});
			return await completion.Task.ConfigureAwait(false);
		}

		public Task<bool> ReplyAsync(uint messageId, SecsMessage message, CancellationToken cancellationToken)
		{
			try
			{
				if (message == null)
				{
					LogWarningSafely("SECS reply skipped because the message is null.");
					return Task.FromResult(false);
				}

				message.ReplyExpected = false;
				var item = message.SecsItem?.ToSecsLiteItem();
				var result = ReplyMessage(message.S, message.F, item, messageId);
				LogMessageOutSafely(message, unchecked((int)messageId));
				return Task.FromResult(result != 0 || messageId != 0);
			}
			catch (Exception ex)
			{
				LogErrorSafely("SECS reply failed.", message, ex);
				return Task.FromResult(false);
			}
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
			var completed = 0;
			lock (_pendingReplyLock)
			{
				_disconnectVersion++;
				_earlyReplies.Clear();
				foreach (var messageId in _pendingReplies.Keys)
				{
					if (_pendingReplies.TryRemove(messageId, out var pending))
					{
						// Preserve the existing missing-reply contract for legacy async-void callers.
						pending.TrySetResult(null);
						completed++;
					}
				}
			}
			if (completed > 0)
			{
				LogWarningSafely($"SECS disconnected; completed {completed} pending replies without a reply.");
			}
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

			LogMessageInSafely(message, unchecked((int)messageId));

			// Odd functions are primary messages (except S9 errors). An equipment event
			// can share an id with a host query and must never complete that query.
			if (func % 2 == 0 || stream == 9)
			{
				message.ReplyExpected = false;
				var matched = CompleteOrBufferReply(messageId, message);
				if (!matched)
				{
					LogWarningSafely($"Unmatched SECS reply S{stream}F{func}, message id 0x{messageId:X8}; ignored.");
				}
				return SecsErrorCode.Success;
			}

			_primaryMessages.Writer.TryWrite(new PrimaryMessageWrapper(this, message, messageId));
			return SecsErrorCode.Success;
		}

		public override void OnMessageReplyTimeout(uint messageId)
		{
			base.OnMessageReplyTimeout(messageId);
			if (CompleteOrBufferReply(messageId, null))
			{
				// A timeout is reported as a missing reply so callers can handle it
				// through their normal null/result checks.  Do not fault an async event
				// callback with an exception.
				LogWarningSafely($"SECS message 0x{messageId:X8} reply timeout.");
			}
		}

		private bool CompleteOrBufferReply(uint messageId, SecsMessage reply)
		{
			lock (_pendingReplyLock)
			{
				if (_pendingReplies.TryRemove(messageId, out var pending))
				{
					pending.TrySetResult(reply);
					return true;
				}
				if (_sendsInProgress > 0 && _earlyReplies.Count < MaxEarlyReplies)
				{
					// First terminal result wins, including a timeout during native send.
					_earlyReplies.TryAdd(messageId, reply);
					return true;
				}
				return false;
			}
		}
	}
}
