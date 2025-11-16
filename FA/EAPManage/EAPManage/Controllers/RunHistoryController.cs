using EAPManage.DataBase;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RunHistoryController : ControllerBase
    {

        [HttpGet]
        public IActionResult Get(string eqpId, string eqpType)
        {
            List<RunHistory> runHistories = DataFactory.Current.RunHistory.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                runHistories = runHistories.Where(a => a.eqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(eqpType))
            {
                runHistories = runHistories.Where(a => a.eqpType == eqpType).ToList();
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", runHistories.Count);
            resdata.Add("list", runHistories);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpDelete]
        public IActionResult Delete([FromBody] List<int> Ids)
        {
            try
            {
                var res = false;
                foreach (var Id in Ids)
                {
                   res= DataFactory.Current.RunHistory.DeleteById(Id);
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除设备失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除设备失败" });
            }
        }
    }
}
