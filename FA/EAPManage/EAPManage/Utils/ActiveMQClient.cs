using System;
using System.Collections.Concurrent;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Util.Synchronization;
using Apache.NMS.Util;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using ISession = Apache.NMS.ISession;
namespace EAPManage.Utils
{

    public class ActiveMQClient : IDisposable
    {
        private readonly string _brokerUri = "failover:tcp://localhost:61616" +
                     "?transport.sendTimeout=5000" +
                    "&transport.useLogging=true";
        private readonly string _sendQueueName = "FA_M_E";
        private readonly string _receiveQueueName = "FA_E_M";
        private readonly int _maxRetries = 5;
        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(1);
        private readonly double _backoffFactor = 2.0;

        private IConnection _connection;
        private ISession _session;
        private IMessageProducer _producer;
        private IMessageConsumer _consumer;
        private int _currentRetries;
        private bool _disposed;
        private readonly ConcurrentDictionary<string, (TaskCompletionSource<IMessage> tcs, CancellationTokenSource cts)> _pendingRequests = new();
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _syncPendingRequests = new();
        private readonly ConcurrentDictionary<string, IMessage> _syncResponses = new();

        public event Action ConnectionInterrupted;
        public event Action ConnectionRestored;
        public event Action PermanentFailure;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        private static readonly Lazy<ActiveMQClient> Lazy = new Lazy<ActiveMQClient>(() => new ActiveMQClient());
        public static ActiveMQClient Current => Lazy.Value;

        /// <summary>
        /// MQ连接初始化
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            var factory = new ConnectionFactory(_brokerUri);
            _connection = await Task.Run(() => factory.CreateConnection());
            _connection.ConnectionInterruptedListener += OnConnectionInterrupted;
            _connection.ConnectionResumedListener += OnConnectionResumed;
            await StartConnectionAsync();
        }

        /// <summary>
        /// 开始尝试连接MQ
        /// </summary>
        /// <returns></returns>
        private async Task StartConnectionAsync()
        {
            try
            {
                await Task.Run(() => _connection.Start());
                _session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);

                var sendDestination = SessionUtil.GetQueue(_session, _sendQueueName);
                _producer = _session.CreateProducer(sendDestination);
                // 设置生产者的消息生存时间为30秒
                _producer.TimeToLive = TimeSpan.FromSeconds(30);

                var receiveDestination = SessionUtil.GetQueue(_session, _receiveQueueName);
                _consumer = _session.CreateConsumer(receiveDestination);
                _consumer.Listener += OnMessageReceived;

                _currentRetries = 0;
                ConnectionRestored?.Invoke();
            }
            catch (Exception ex)
            {
                await HandleConnectionFailureAsync(ex);
            }
        }


        /// <summary>
        /// 连接断开
        /// </summary>
        private void OnConnectionInterrupted()
        {
            ConnectionInterrupted?.Invoke();
            _ = HandleConnectionFailureAsync(new Exception("Connection interrupted"));
        }

        private void OnConnectionResumed()
        {
            // ActiveMQ会自动处理重连，这里可以添加额外的恢复逻辑
        }


        /// <summary>
        /// 连接断开重连处理
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private async Task HandleConnectionFailureAsync(Exception ex)
        {
            if (_currentRetries >= _maxRetries)
            {
                PermanentFailure?.Invoke();
                return;
            }

            var delay = CalculateBackoffDelay(_currentRetries);
            _currentRetries++;

            await Task.Delay(delay);
            await StartConnectionAsync();
        }

        private TimeSpan CalculateBackoffDelay(int retryCount)
        {
            var delayTicks = (double)_initialDelay.Ticks * Math.Pow(_backoffFactor, retryCount);
            return TimeSpan.FromTicks((long)delayTicks);
        }

        // 异步发送方法
        public async Task<T> SendRequestAsync<T>(string messageBody) where T : class
        {
            if (_session == null || _producer == null)
                return default;
            TimeSpan timeout = TimeSpan.FromSeconds(30);
            var correlationId = Guid.NewGuid().ToString();
            var message = _session.CreateTextMessage(messageBody);
            var tcs = new TaskCompletionSource<IMessage>();

            message.NMSCorrelationID = correlationId;
            message.NMSReplyTo = SessionUtil.GetQueue(_session, _receiveQueueName);
            try
            {
                // 注册等待
                _pendingRequests[correlationId] = (tcs, new CancellationTokenSource());

                // 发送消息
                _producer.Send(message);

                // 等待响应或超时
                var timeoutTask = Task.Delay(timeout);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 超时
                    _pendingRequests.TryRemove(correlationId, out _);
                    timeoutTask.Dispose(); // 手动释放 timeoutTask
                    return null;
                }

                // 返回结果
                if (tcs.Task.Result is ITextMessage responseMessage)
                {
                    var mqHeader = XmlSerialization.DeserializeNodeObj<MQHeader>(responseMessage.Text, "Response", "Header");
                    if (mqHeader.RESULT != MQResult.SUCCESS.ToString())
                    {
                        return null;
                    }
                    else
                    {
                        return XmlSerialization.DeserializeNodeObj<T>(responseMessage.Text, "Response", "Body");
                    }
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                _pendingRequests.TryRemove(correlationId, out _);
            }
        }
        public void SendAsync(string messageBody)
        {
            try
            {
                if (_session == null || _producer == null) return;
                var message = _session.CreateTextMessage(messageBody);
                _producer.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ActiveMQ SendAsync Error] {ex.Message}");
            }
        }

        private void OnMessageReceived(IMessage receivedMessage)
        {
            // 处理回复的消息
            if (!string.IsNullOrEmpty(receivedMessage.NMSCorrelationID))
            {
                if (_pendingRequests.TryRemove(receivedMessage.NMSCorrelationID, out var entry))
                {
                    entry.cts.Cancel();
                    entry.tcs.TrySetResult(receivedMessage);
                    return;
                }
            }

            // 处理请求的消息
            if (!string.IsNullOrEmpty(receivedMessage.NMSCorrelationID))
            {
                if (_syncPendingRequests.TryGetValue(receivedMessage.NMSCorrelationID, out var mre))
                {
                    _syncResponses[receivedMessage.NMSCorrelationID] = receivedMessage;
                    mre.Set();
                    return;
                }
            }

            // 触发普通消息事件
            var eventArgs = new MessageReceivedEventArgs(receivedMessage, this);
            MessageReceived?.Invoke(this, eventArgs);
        }

        public void SendReply(IMessage originalMessage, string replyBody)
        {
            if (originalMessage.NMSReplyTo == null)
            {
                throw new InvalidOperationException("Original message has no ReplyTo destination");
            }

            var replyMessage = _session.CreateTextMessage(replyBody);
            replyMessage.NMSCorrelationID = originalMessage.NMSCorrelationID;

            using var producer = _session.CreateProducer(originalMessage.NMSReplyTo);
            producer.Send(replyMessage);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _consumer?.Close();
            _producer?.Close();
            _session?.Close();
            _connection?.Close();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    // 新增事件参数类
    public class MessageReceivedEventArgs : EventArgs
    {
        public IMessage Message { get; }
        public ActiveMQClient Tool { get; }

        public MessageReceivedEventArgs(IMessage message, ActiveMQClient tool)
        {
            Message = message;
            Tool = tool;
        }

        public void Reply(string messageBody)
        {
            Tool.SendReply(Message, messageBody);
        }
    }
}