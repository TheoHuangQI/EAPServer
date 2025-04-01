using Apache.NMS.ActiveMQ;
using Apache.NMS.Util;
using Apache.NMS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apache.NMS.ActiveMQ.Commands;
using EAP.HC.Core.Entities.MES;

namespace EAP.HC.Core
{

    public class ActiveMQClient : IDisposable
    {
        private readonly string _brokerUri = "failover:tcp://localhost:61616" +
                     "?transport.sendTimeout=5000" +
                    "&transport.useLogging=true";
        private readonly string _sendQueueName = "FA_E_M";
        private readonly string _receiveQueueName = "FA_M_E";
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
        public async Task InitializeAsync()
        {
            var factory = new ConnectionFactory(_brokerUri);
            _connection = await Task.Run(() => factory.CreateConnection());
            _connection.ConnectionInterruptedListener += OnConnectionInterrupted;
            _connection.ConnectionResumedListener += OnConnectionResumed;

            await StartConnectionAsync();
        }

        private async Task StartConnectionAsync()
        {
            try
            {
                await Task.Run(() => _connection.Start());
                _session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);

                var sendDestination = SessionUtil.GetQueue(_session, _sendQueueName);
                _producer = _session.CreateProducer(sendDestination);

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

        private void OnConnectionInterrupted()
        {
            ConnectionInterrupted?.Invoke();
            _ = HandleConnectionFailureAsync(new Exception("Connection interrupted"));
        }

        private void OnConnectionResumed()
        {
            // ActiveMQ会自动处理重连，这里可以添加额外的恢复逻辑
        }

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

        // 修改后的异步发送方法（带超时）
        public async Task<string> SendRequestAsync(string messageBody)
        {
            var correlationId = Guid.NewGuid().ToString();
            var message = _session.CreateTextMessage(messageBody);
            var tcs = new TaskCompletionSource<IMessage>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            message.NMSCorrelationID = correlationId;
            message.NMSReplyTo = SessionUtil.GetQueue(_session, _receiveQueueName);

            try
            {
                _pendingRequests[correlationId] = (tcs, cts);
                cts.Token.Register(() =>
                {
                    if (_pendingRequests.TryRemove(correlationId, out var entry))
                    {
                        entry.tcs.TrySetCanceled();
                    }
                });

                _producer.Send(message);
                var response= await tcs.Task.WaitAsync(cts.Token);
                if (response is ITextMessage responseMessage)
                {
                   return responseMessage.Text;
                }
                else
                {
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Request timed out after {30}s");
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                _pendingRequests.TryRemove(correlationId, out _);
            }
        }

        // 同步发送方法
        public T SendRequest<T>(string messageBody, out MQHeader replyHeader) where T : class
        {
            TimeSpan timeout = TimeSpan.FromSeconds(30);
            var correlationId = Guid.NewGuid().ToString();
            var message = _session.CreateTextMessage(messageBody);
            var mre = new ManualResetEventSlim(false);

            message.NMSCorrelationID = correlationId;
            message.NMSReplyTo = SessionUtil.GetQueue(_session, _receiveQueueName);
            replyHeader = new MQHeader();
            T t = null;
            try
            {
                // 注册等待
                _syncPendingRequests[correlationId] = mre;
                _syncResponses[correlationId] = null;

                // 发送消息
                _producer.Send(message);

                // 等待信号
                if (!mre.Wait(timeout))
                {
                    throw new TimeoutException($"请求超时 ({timeout.TotalSeconds}s)");
                }
              
                // 返回结果
                if (_syncResponses.TryRemove(correlationId, out var response))
                {
                    if (response is ITextMessage responseMessage)
                    {
                        replyHeader = XmlSerialization.DeserializeNodeObj<MQHeader>(responseMessage.Text, "Response", "Header");
                        if (replyHeader.RESULT != MQResult.SUCCESS.ToString())
                        {
                            return null;
                        }
                        else
                        {
                            t = XmlSerialization.DeserializeNodeObj<T>(responseMessage.Text, "Response", "Body");
                            return t;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                throw new InvalidOperationException("响应数据丢失");
            }
            catch(Exception ex)
            {
                Console.Write(ex.Message);
                return null;
            }
            finally
            {
                _syncPendingRequests.TryRemove(correlationId, out _);
                _syncResponses.TryRemove(correlationId, out _);
                mre.Dispose();
            }
        }


        // 修改消息接收处理
        private void OnMessageReceived(IMessage receivedMessage)
        {
            // 处理同步请求
            if (!string.IsNullOrEmpty(receivedMessage.NMSCorrelationID))
            {
                if (_syncPendingRequests.TryGetValue(receivedMessage.NMSCorrelationID, out var mre))
                {
                    _syncResponses[receivedMessage.NMSCorrelationID] = receivedMessage;
                    mre.Set();
                    return;
                }
            }

            // 处理异步请求（原有逻辑）
            if (!string.IsNullOrEmpty(receivedMessage.NMSCorrelationID))
            {
                if (_pendingRequests.TryRemove(receivedMessage.NMSCorrelationID, out var entry))
                {
                    entry.cts.Cancel();
                    entry.tcs.TrySetResult(receivedMessage);
                    return;
                }
            }

            // 触发普通消息事件
            var eventArgs = new MessageReceivedEventArgs(receivedMessage, this);
            MessageReceived?.Invoke(this, eventArgs);
        }

        //// 增强的消息接收处理
        //private void OnMessageReceived(IMessage receivedMessage)
        //{
        //    try
        //    {
        //        // 处理请求/响应模式
        //        if (!string.IsNullOrEmpty(receivedMessage.NMSCorrelationID))
        //        {
        //            if (_pendingRequests.TryRemove(receivedMessage.NMSCorrelationID, out var entry))
        //            {
        //                entry.cts.Cancel(); // 取消超时计时器
        //                entry.tcs.TrySetResult(receivedMessage);
        //                return;
        //            }
        //        }
        //        // 触发消息接收事件（包含回复能力）
        //        var eventArgs = new MessageReceivedEventArgs(receivedMessage, this);
        //        MessageReceived?.Invoke(this, eventArgs);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error processing message: {ex}");
        //    }
        //}

        // 新增回复方法
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
