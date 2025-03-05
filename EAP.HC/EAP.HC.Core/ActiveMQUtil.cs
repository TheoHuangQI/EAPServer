using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.Util;

namespace EAP.HC.Core
{
    public class ActiveMQUtil
    {
        IConnectionFactory factory;
        IConnection connection;
        ISession session;
        string brokerUri = "activemq:tcp://localhost:61616";  // ActiveMQ 服务器地址
        string queueName = "test.queue";

        public ActiveMQUtil()
        {
            factory = new ConnectionFactory(brokerUri);
            connection = factory.CreateConnection("admin", "admin");
            session = connection.CreateSession();
            connection.Start();
        }
        public void SendMessage(int i)
        {
            try
            {
                IDestination destination = SessionUtil.GetDestination(session, $"queue://{queueName}");
                using (IMessageProducer producer = session.CreateProducer(destination))
                {
                    producer.DeliveryMode = MsgDeliveryMode.Persistent; // 持久化消息

                    string messageText = $"Hello ActiveMQ!:  {i}";
                    ITextMessage message = session.CreateTextMessage(messageText);
                    producer.Send(message);
                    Console.WriteLine($"已发送消息: {messageText}");
                }
            }
            catch (NMSException ex)
            {
                Console.WriteLine($"NMS 异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异常: {ex.Message}");
            }
        }

        public async void ReceiveMessage()
        {
            try
            {
                IDestination destination = SessionUtil.GetDestination(session, $"queue://{queueName}");
                using (IMessageConsumer consumer = session.CreateConsumer(destination))
                {
                    // 异步接收方式
                    consumer.Listener += (message) =>
                    {
                        if (message is ITextMessage textMessage)
                        {
                            Console.WriteLine($"收到消息: {textMessage.Text}");
                        }
                    };
                    Console.WriteLine("等待接收消息... (按任意键退出)");
                    Console.ReadKey();
                }
            }
            catch (NMSException ex)
            {
                Console.WriteLine($"NMS 异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异常: {ex.Message}");
            }
        }
    }
}
