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

            var result = dataRules.Select(rule => {
                var actions = new List<int>();
                if (rule.action.Contains("StopEqp"))
                    actions.Add(0);
                if (rule.action.Contains("HoldLot"))
                    actions.Add(1);

                return new {
                    id = rule.Id,
                    vid = rule.vid,
                    vidType = rule.vidType,
                    eqpId = rule.eqpId,
                    interval = rule.interval,
                    count = rule.count,
                    triggerType = rule.triggerType,
                    startEvent = rule.startEvent,
                    endEvent = rule.endEvent,
                    compareType = rule.compareType,
                    compareValue = rule.compareValue,
                    compareMax = rule.compareMax,
                    compareMin = rule.compareMin,
                    allowNumber = rule.limitTimes,
                    action = rule.action,
                    actions = actions
                };
            }).ToList();

            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", result.Count);
            resdata.Add("list", result);
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