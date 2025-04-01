using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.Util;

namespace EAP.HC.Core
{
    public class ActiveMQUtil
    {
        private IConnectionFactory factory;
        private IConnection connection;
        private ISession session;
        private IMessageConsumer consumer;
        private string brokerUri = "activemq:tcp://localhost:61616";
        private readonly string SendQueueName = "FA_E_M";
        private readonly string ReceiveQueueName = "FA_M_E";
        private bool isListening = false;
        private readonly object _reconnectLock = new object();
        private static readonly Lazy<ActiveMQUtil> Lazy = new Lazy<ActiveMQUtil>(() => new ActiveMQUtil());
        public static ActiveMQUtil Current => Lazy.Value;

        // 指数退避配置             
        private const int MaxRetryAttempts = 5;          // 最大重试次数
        private const double BaseDelaySeconds = 1.0;     // 基础等待时间
        private const double MaxDelaySeconds = 30.0;     // 最大等待时间
        private int _currentRetryCount = 0;              // 当前重试次数
        private bool _isInRetry = false;                 // 重试状态标志

        // 定义消息委托类型
        public delegate void MessageHandler(string messageBody, IMessage message);
        public delegate void ErrorHandler(Exception ex, string context);

        // 声明事件
        public event MessageHandler OnMessageReceived;
        public event ErrorHandler OnErrorOccurred;



        public ActiveMQUtil()
        {
            
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            factory = new ConnectionFactory(brokerUri);
            connection = factory.CreateConnection("admin", "admin");
            session = connection.CreateSession();
            // 添加连接状态监听
            connection.ConnectionInterruptedListener += OnConnectionInterrupted;
            connection.ConnectionResumedListener += OnConnectionResumed;

            connection.Start();
            StartListening();
        }

        // 连接中断回调
        private void OnConnectionInterrupted()
        {
            Console.WriteLine("检测到连接中断！");
            Reconnect();
        }

        // 连接恢复回调
        private void OnConnectionResumed()
        {
            Console.WriteLine("连接已恢复");
        }

        //带确认的发送方法
        public bool SendMessage(string msg, out string error)
        {
            error = null;
            try
            {
                IDestination destination = SessionUtil.GetDestination(session, $"queue://{SendQueueName}");
                using (var producer = session.CreateProducer(destination))
                {
                    var message = session.CreateTextMessage(msg);
                    message.NMSReplyTo = SessionUtil.GetDestination(session, $"queue://{ReceiveQueueName}");
                    message.NMSCorrelationID = Guid.NewGuid().ToString();
                    producer.Send(message);
                    return true;
                }
            }
            catch (NMSException ex)
            {
                error = $"NMS错误: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"系统错误: {ex.Message}";
                return false;
            }
        }

        public string WaitForResponse(string correlationId, int timeoutMs = 30000)
        {
            IDestination replyQueue = SessionUtil.GetDestination(session, $"queue://{ReceiveQueueName}");
            using (IMessageConsumer consumer = session.CreateConsumer(replyQueue, $"NMSCorrelationID = '{correlationId}'"))
            {
                IMessage reply = consumer.Receive(TimeSpan.FromMilliseconds(timeoutMs));

                if (reply is ITextMessage textReply)
                {
                    return textReply.Text;
                }
                throw new TimeoutException("等待回复超时");
            }
        }

        // 接收监听方法
        public void StartListening()
        {
            if (isListening) return;

            isListening = true;
            new Thread(() =>
            {
                while (isListening)
                {
                    try
                    {
                        var destination = SessionUtil.GetDestination(session, $"queue://{ReceiveQueueName}");
                        consumer = session.CreateConsumer(destination);

                        consumer.Listener += message =>
                        {
                            try
                            {
                                if (message is ITextMessage textMsg)
                                {
                                    // 触发消息接收事件
                                    OnMessageReceived?.Invoke(textMsg.Text, message);

                                    // 自动确认消息（可根据需求修改）
                                    if (session.AcknowledgementMode == AcknowledgementMode.ClientAcknowledge)
                                    {
                                        message.Acknowledge();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 触发错误事件
                                OnErrorOccurred?.Invoke(ex, "处理消息时发生异常");
                            }
                        };

                        while (isListening) Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred?.Invoke(ex, "监听循环异常");
                    }
                }
            }).Start();
        }

        public void StopListening()
        {
            isListening = false;
            consumer?.Close();
        }

        public void Reconnect()
        {
            lock (_reconnectLock)
            {
                if (_isInRetry) return;
                _isInRetry = true;

                try
                {
                    while (_currentRetryCount <= MaxRetryAttempts)
                    {
                        try
                        {
                            Console.WriteLine($"尝试第 {_currentRetryCount + 1} 次重连...");

                            // 释放旧资源
                            consumer?.Close();
                            session?.Close();
                            connection?.Close();

                            // 重建连接
                            connection = factory.CreateConnection("admin", "admin");
                            connection.ConnectionInterruptedListener += OnConnectionInterrupted;
                            connection.ConnectionResumedListener += OnConnectionResumed;

                            session = connection.CreateSession();
                            connection.Start();

                            // 重置计数器
                            _currentRetryCount = 0;
                            _isInRetry = false;
                            Console.WriteLine("重连成功！");

                            // 重建消费者
                            if (isListening)
                            {
                                isListening = false;
                                StartListening();
                                Console.WriteLine("监听已重建");
                            }
                            return;
                        }
                        catch (Exception ex)
                        {
                            _currentRetryCount++;
                            if (_currentRetryCount > MaxRetryAttempts)
                            {
                                Console.WriteLine($"达到最大重试次数 {MaxRetryAttempts} 次，停止重连");
                                HandlePermanentFailure();
                                return;
                            }

                            // 计算等待时间
                            var delay = CalculateExponentialDelay();
                            Console.WriteLine($"等待 {delay.TotalSeconds} 秒后重试...");
                            Thread.Sleep(delay);
                        }
                    }
                }
                finally
                {
                    _isInRetry = false;
                }
            }
        }

        // 指数退避算法
        private TimeSpan CalculateExponentialDelay()
        {
            double delaySeconds = Math.Min(
                MaxDelaySeconds,
                BaseDelaySeconds * Math.Pow(2, _currentRetryCount - 1)
            );

            // 添加随机抖动避免同步重试
            Random rand = new Random();
            delaySeconds *= rand.NextDouble() * 0.3 + 0.85; // 抖动范围 85%~115%

            return TimeSpan.FromSeconds(delaySeconds);
        }

        // 永久失败处理
        private void HandlePermanentFailure()
        {
            Console.WriteLine("进入永久失败处理模式");
            // 触发警报、记录日志、切换备用服务器等
            // 示例：每5分钟尝试一次恢复
            Timer recoveryTimer = new Timer(_ =>
            {
                Console.WriteLine("尝试恢复连接...");
                _currentRetryCount = 0;
                Reconnect();
            }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        }
    }
}
