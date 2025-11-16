using Microsoft.AspNetCore.SignalR;

namespace EAPManage.Models
{
    public class LogService
    {
        private readonly IHubContext<LogHub> _hubContext;

        public LogService(IHubContext<LogHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void SendMessage(string eqpId,string portId,string type, string message,string msgType)
        {
             _hubContext.Clients.All.SendAsync("ReceiveLog", new
            {
                time = DateTime.Now.ToString("HH:mm:ss"),
                type,
                message,
                portId,
                eqpId,
                msgType
             });
        }
    }
}
