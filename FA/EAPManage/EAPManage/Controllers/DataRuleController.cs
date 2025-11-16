using EAPManage.DataBase;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataRuleController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId, string vid)
        {
            List<DataRule> dataRules = DataFactory.Current.DataRule.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                dataRules = dataRules.Where(a => a.eqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(vid))
            {
                dataRules = dataRules.Where(a => a.vid == vid).ToList();
            }
            
            foreach (var rule in dataRules)
            {
                rule.actions = new List<int>();
                if (rule.action.Contains("StopEqp"))
                    rule.actions.Add(0);
                if(rule.action.Contains("HoldLot"))
                    rule.actions.Add(1);
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", dataRules.Count);
            resdata.Add("list", dataRules);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }


        [HttpPost]
        public IActionResult Post([FromBody] DataRule dataRule)
        {
            try
            {
                var res = DataFactory.Current.DataRule.Insert(dataRule);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加采集规则失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "添加采集规则失败" });
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] DataRule dataRule)
        {
            try
            {
                var res = DataFactory.Current.DataRule.Update(dataRule);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新采集规则失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "更新采集规则失败" });
            }
        }


        [HttpDelete]
        public IActionResult Delete([FromBody] List<int> Ids)
        {
            try
            {
                var res = false;
                foreach (var Id in Ids)
                {
                    res = DataFactory.Current.DataRule.DeleteById(Id);
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除采集规则失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除采集规则失败" });
            }
        }
    }
}
