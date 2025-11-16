using Microsoft.AspNetCore.SignalR;

namespace EAPManage.Models
{
    public class LogHub : Hub
    {
        // 向所有客户端发送日志消息
        public async Task SendLog(string time, string type, string message)
        {
            await Clients.All.SendAsync("ReceiveLog", new
            {
                time,
                type,
                message
            });
        }
    }
}
