using EAPManage.DataBase;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataHistoryController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId, string vid)
        {
            List<DataHistory> DataHistorys = DataFactory.Current.DataHistory.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                DataHistorys = DataHistorys.Where(a => a.eqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(vid))
            {
                DataHistorys = DataHistorys.Where(a => a.vid == vid).ToList();
            }

            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", DataHistorys.Count);
            resdata.Add("list", DataHistorys);
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
                    res = DataFactory.Current.DataHistory.DeleteById(Id);
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除采集记录失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除采集记录失败" });
            }
        }
    }
}
