using Microsoft.Extensions.Options;
using SecsLite.Model;

namespace Secs4Net
{
	public sealed class HsmsConnection
	{
		private static readonly TimeSpan[] RetryDelays =
		{
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(2),
			TimeSpan.FromSeconds(5),
			TimeSpan.FromSeconds(10),
			TimeSpan.FromSeconds(30),
			TimeSpan.FromSeconds(60),
		};

		private readonly SecsGemOptions _options;
		private readonly ISecsGemLogger _logger;
		private readonly object _stateLock = new object();
		private CancellationToken _cancellationToken;
		private int _reconnectInProgress;
		private long _selectWaitVersion;
		private int _retryCount;
		private DateTimeOffset? _downtimeStartedAt;
		private DateTimeOffset? _lastAttemptTime;
		private DateTimeOffset? _lastErrorTime;
		private DateTimeOffset? _nextRetryTime;
		private string _lastError = string.Empty;
		private int _lastErrorCode;
		private DisconnectReason _lastDisconnectReason = DisconnectReason.Unknown;
		private bool _manualStopRequested;
		private bool _stoppingForReconnect;

		internal FaSecsSession Session { get; }

		public HsmsConnection(IOptions<SecsGemOptions> options, ISecsGemLogger logger)
		{
			_options = options.Value;
			_logger = logger;
			Session = new FaSecsSession(this, logger);
			State = ConnectionState.Disconnected;
		}

		public event EventHandler<ConnectionState> ConnectionChanged;

		public ConnectionState State { get; private set; }

		public bool LinkTestEnabled { get; set; }

		public bool HasConnected { get; private set; }

		public bool IsTransportConnected => Session.IsConnected;

		public bool IsSelected => Session.IsSelected;

		public long SessionId { get; private set; }

		public bool AutoReconnect => _options.AutoReconnect;

		public string RemoteAddress => $"{_options.IpAddress}:{_options.Port}";

		public int RetryCount
		{
			get
			{
				lock (_stateLock)
				{
					return _retryCount;
				}
			}
		}

		public string LastError
		{
			get
			{
				lock (_stateLock)
				{
					return _lastError;
				}
			}
		}

		public int LastErrorCode
		{
			get
			{
				lock (_stateLock)
				{
					return _lastErrorCode;
				}
			}
		}

		public DateTimeOffset? LastErrorTime
		{
			get
			{
				lock (_stateLock)
				{
					return _lastErrorTime;
				}
			}
		}

		public DateTimeOffset? LastAttemptTime
		{
			get
			{
				lock (_stateLock)
				{
					return _lastAttemptTime;
				}
			}
		}

		public DateTimeOffset? NextRetryTime
		{
			get
			{
				lock (_stateLock)
				{
					return _nextRetryTime;
				}
			}
		}

		public DisconnectReason LastDisconnectReason
		{
			get
			{
				lock (_stateLock)
				{
					return _lastDisconnectReason;
				}
			}
		}

		public bool CanReconnectNow
		{
			get
			{
				var nextRetryTime = NextRetryTime;
				return !nextRetryTime.HasValue || nextRetryTime.Value <= DateTimeOffset.Now;
			}
		}

		public void Start(CancellationToken cancellationToken = default)
		{
			_cancellationToken = cancellationToken;
			_manualStopRequested = false;
			TryStart();
		}

		public void Stop(DisconnectReason reason = DisconnectReason.UserRequested)
		{
			_manualStopRequested = reason == DisconnectReason.UserRequested || reason == DisconnectReason.ApplicationShutdown;
			Interlocked.Increment(ref _selectWaitVersion);
			ClearRetrySchedule();

			try
			{
				Session.Stop();
			}
			catch (Exception ex)
			{
				_logger?.Warning($"FA_Base HSMS stop failed. DeviceId: {_options.DeviceId}, Reason: {reason}, Error: {ex.Message}");
			}

			SetLastDisconnect(reason);
			SetState(_manualStopRequested ? ConnectionState.Disabled : ConnectionState.Disconnected);
		}

		public void Reconnect()
		{
			Reconnect(force: false);
		}

		public void ManualReconnect()
		{
			_manualStopRequested = false;
			ClearRetrySchedule();
			Reconnect(force: true);
		}

		private bool TryStart()
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				SetLastDisconnect(DisconnectReason.ApplicationShutdown);
				SetState(ConnectionState.Disabled);
				return false;
			}

			RecordAttempt();
			Interlocked.Increment(ref _selectWaitVersion);
			SetState(ConnectionState.Connecting);

			var config = new HsmsConfig
			{
				DeviceID = _options.DeviceId,
				Mode = _options.IsActive ? SecsLite.HsmsMode.Active : SecsLite.HsmsMode.Passive,
				LocalIP = _options.IsActive ? _options.LocalIpAddress : _options.IpAddress,
				LocalPort = _options.IsActive ? (ushort)_options.LocalPort : (ushort)_options.Port,
				RemoteIP = _options.IsActive ? _options.IpAddress : string.Empty,
				RemotePort = _options.IsActive ? (ushort)_options.Port : (ushort)0,
				T3 = (uint)_options.T3,
				T5 = (uint)_options.T5,
				T6 = (uint)_options.T6,
				T7 = (uint)_options.T7,
				T8 = (uint)_options.T8,
				LinkTestInterval = (uint)_options.LinkTestInterval,
			};

			try
			{
				Session.Start(config);
				if (_options.IsActive && !Session.IsConnected)
				{
					RegisterFailure(
						DisconnectReason.ConnectFailed,
						Session.LastStartupErrorCode == 0 ? 1 : Session.LastStartupErrorCode,
						string.IsNullOrWhiteSpace(Session.LastStartupError) ? "Failed to connect to HSMS server" : Session.LastStartupError);
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				RegisterFailure(
					DisconnectReason.Unknown,
					0,
					$"{ex.GetType().Name}: {ex.Message}");
				_logger?.Error($"FA_Base HSMS startup failed. DeviceId: {_options.DeviceId}, Remote: {RemoteAddress}, State: {State}, RetryCount: {RetryCount}.", ex);
				return false;
			}
		}

		private void Reconnect(bool force)
		{
			if (_cancellationToken.IsCancellationRequested)
			{
				return;
			}

			if (!_options.AutoReconnect && !force)
			{
				return;
			}

			if (!force && !CanReconnectNow)
			{
				return;
			}

			if (Interlocked.Exchange(ref _reconnectInProgress, 1) == 1)
			{
				return;
			}

			try
			{
				SetState(ConnectionState.Reconnecting);
				try
				{
					_stoppingForReconnect = true;
					Session.Stop();
				}
				catch (Exception ex)
				{
					_logger?.Warning($"FA_Base HSMS stop during reconnect failed: {ex.Message}");
				}
				finally
				{
					_stoppingForReconnect = false;
				}

				TryStart();
			}
			finally
			{
				Interlocked.Exchange(ref _reconnectInProgress, 0);
			}
		}

		internal void OnConnected()
		{
			SessionId++;
			HasConnected = true;
			SetState(ConnectionState.Connected);
			if (_options.IsActive)
			{
				SetState(ConnectionState.Selecting);
			}
			WaitForSelectedAsync(SessionId, _cancellationToken);
		}

		internal void OnDisconnected()
		{
			SessionId++;
			Interlocked.Increment(ref _selectWaitVersion);

			if (_manualStopRequested || _cancellationToken.IsCancellationRequested)
			{
				SetLastDisconnect(_cancellationToken.IsCancellationRequested
					? DisconnectReason.ApplicationShutdown
					: DisconnectReason.UserRequested);
				SetState(ConnectionState.Disabled);
				return;
			}

			if (_stoppingForReconnect)
			{
				SetState(ConnectionState.Reconnecting);
				return;
			}

			RegisterFailure(DisconnectReason.RemoteClosed, 0, "Remote disconnected");
		}

		private void SetState(ConnectionState state, bool forceNotify = false)
		{
			EventHandler<ConnectionState> handler;
			lock (_stateLock)
			{
				if (State == state && !forceNotify)
				{
					return;
				}
				State = state;
				handler = ConnectionChanged;
			}
			handler?.Invoke(this, state);
		}

		private void RecordAttempt()
		{
			lock (_stateLock)
			{
				_lastAttemptTime = DateTimeOffset.Now;
			}
		}

		private void SetLastDisconnect(DisconnectReason reason)
		{
			lock (_stateLock)
			{
				_lastDisconnectReason = reason;
			}
		}

		private void RegisterFailure(DisconnectReason reason, int errorCode, string error)
		{
			var now = DateTimeOffset.Now;
			TimeSpan delay = TimeSpan.Zero;
			int retryCount = 0;
			DateTimeOffset? nextRetryTime = null;
			var shouldReconnect = _options.AutoReconnect && !_manualStopRequested && !_cancellationToken.IsCancellationRequested;

			lock (_stateLock)
			{
				_lastDisconnectReason = reason;
				_lastErrorCode = errorCode;
				_lastError = string.IsNullOrWhiteSpace(error) ? reason.ToString() : error;
				_lastErrorTime = now;
				_downtimeStartedAt ??= now;

				if (shouldReconnect)
				{
					_retryCount++;
					retryCount = _retryCount;
					delay = CalculateRetryDelay(retryCount);
					_nextRetryTime = now.Add(delay);
					nextRetryTime = _nextRetryTime;
				}
				else
				{
					_nextRetryTime = null;
				}
			}

			if (shouldReconnect)
			{
				SetState(ConnectionState.Reconnecting, forceNotify: true);
				var message =
					$"[Device:{_options.DeviceId}] Reconnect scheduled. Address={RemoteAddress}, Retry={retryCount}, Delay={FormatDelay(delay)}, NextRetry={nextRetryTime?.ToString("HH:mm:ss")}, ErrorCode={errorCode}, Reason={LastError}";
				Console.WriteLine(message);
				_logger?.Warning(message);
			}
			else
			{
				SetState(ConnectionState.Disconnected);
				var message =
					$"[Device:{_options.DeviceId}] Connection failed and auto reconnect is disabled. Address={RemoteAddress}, ErrorCode={errorCode}, Reason={LastError}";
				Console.WriteLine(message);
				_logger?.Warning(message);
			}
		}

		private void ClearRetrySchedule()
		{
			lock (_stateLock)
			{
				_nextRetryTime = null;
			}
		}

		private void ResetRetryAfterCommunication()
		{
			int previousRetryCount;
			TimeSpan? downtime = null;
			lock (_stateLock)
			{
				previousRetryCount = _retryCount;
				if (_downtimeStartedAt.HasValue)
				{
					downtime = DateTimeOffset.Now - _downtimeStartedAt.Value;
				}

				_retryCount = 0;
				_nextRetryTime = null;
				_lastError = string.Empty;
				_lastErrorCode = 0;
				_lastDisconnectReason = DisconnectReason.Unknown;
				_downtimeStartedAt = null;
			}

			if (previousRetryCount > 0)
			{
				var message =
					$"[Device:{_options.DeviceId}] Connection restored. Downtime={FormatDuration(downtime)}, RetryCount={previousRetryCount}";
				Console.WriteLine(message);
				_logger?.Info(message);
			}
		}

		private static TimeSpan CalculateRetryDelay(int retryCount)
		{
			var index = Math.Max(0, Math.Min(retryCount - 1, RetryDelays.Length - 1));
			return RetryDelays[index];
		}

		private static string FormatDelay(TimeSpan delay)
		{
			return delay.TotalSeconds >= 1
				? $"{(int)delay.TotalSeconds}s"
				: $"{delay.TotalMilliseconds:0}ms";
		}

		private static string FormatDuration(TimeSpan? duration)
		{
			return duration.HasValue ? duration.Value.ToString(@"hh\:mm\:ss") : "00:00:00";
		}

		internal void OnMessageTraffic()
		{
			if (State == ConnectionState.Selected ||
				State == ConnectionState.Selecting ||
				State == ConnectionState.Connected)
			{
				ResetRetryAfterCommunication();
				SetState(ConnectionState.Communicating);
			}
		}

		private async void WaitForSelectedAsync(long sessionId, CancellationToken cancellationToken)
		{
			var waitVersion = Interlocked.Increment(ref _selectWaitVersion);
			var timeout = TimeSpan.FromMilliseconds(Math.Max(_options.T7, _options.T6));
			if (timeout <= TimeSpan.Zero)
			{
				timeout = TimeSpan.FromSeconds(10);
			}
			var deadline = DateTime.UtcNow + timeout;

			try
			{
				while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
				{
					if (waitVersion != Volatile.Read(ref _selectWaitVersion) || sessionId != SessionId)
					{
						return;
					}
					if (Session.IsConnected && Session.IsSelected)
					{
						SetState(ConnectionState.Selected);
						return;
					}
					await Task.Delay(100, cancellationToken);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}

			if (!cancellationToken.IsCancellationRequested &&
				sessionId == SessionId &&
				State != ConnectionState.Selected &&
				State != ConnectionState.Communicating)
			{
				try
				{
					_stoppingForReconnect = true;
					Session.Stop();
				}
				catch (Exception ex)
				{
					_logger?.Warning($"FA_Base HSMS stop after select timeout failed. DeviceId: {_options.DeviceId}, Remote: {RemoteAddress}, Error: {ex.Message}");
				}
				finally
				{
					_stoppingForReconnect = false;
				}
				RegisterFailure(DisconnectReason.HSMSSelectFailed, 0, "HSMS select wait timeout");
			}
		}
	}
}
