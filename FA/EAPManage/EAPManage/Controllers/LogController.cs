using EAPManage.Models;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly LogService _logService;

        public LogController(LogService logService)
        {
            _logService = logService;
        }

        [HttpPost]
        public async Task<IActionResult> AddLog([FromBody] LogMessage log)
        {
           // await _logService.SendLogAsync(log.Type, log.Content);
            return Ok();
        }
    }

    public class LogMessage
    {
        public string Type { get; set; } // 日志类型：info/warning/error
        public string Content { get; set; } // 日志内容
    }

}
