using EAPManage.DataBase;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExceptionController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId, string eqpType,string state)
        {
            List<ExceptionRecord> exceptions = DataFactory.Current.ExceptionRecord.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                exceptions = exceptions.Where(a => a.eqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(eqpType))
            {
                exceptions = exceptions.Where(a => a.eqpType == eqpType).ToList();
            }

            if (!string.IsNullOrEmpty(state))
            {
                exceptions = exceptions.Where(a => a.state == int.Parse(state)).ToList();
            }

            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", exceptions.Count);
            resdata.Add("list", exceptions);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }


        [HttpPut]
        public IActionResult Put([FromBody] List<int> Ids)
        {
            try
            {
                var res = 0;
                DataFactory.Current.ExceptionRecord.ExecuteTransaction(db =>
                {
                    foreach (var Id in Ids)
                    {
                        res = DataFactory.Current.ExceptionRecord.Update(u => u.Id == Id, u => new ExceptionRecord()
                        {
                            state = 1
                        });
                    }
                });
                if (res>0)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "处理异常记录失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "处理异常记录失败" });
            }
        }


        [HttpDelete]
        public IActionResult Delete([FromBody] ExceptionRecord exec)
        {
            try
            {
                var res = DataFactory.Current.ExceptionRecord.DeleteById(exec.Id);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除异常记录失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除异常记录失败" });
            }
        }

        [HttpDelete]
        [Route("Deletes")]
        public IActionResult Deletes([FromBody] List<int> Ids)
        {
            try
            {
                var res = true;
                if (Ids.Count > 0)
                {
                    DataFactory.Current.ExceptionRecord.ExecuteTransaction(db =>
                    {
                        foreach (var Id in Ids)
                        {
                            var res = DataFactory.Current.ExceptionRecord.DeleteById(Id);
                        }
                    });
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "批量删除异常记录失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "批量删除异常记录失败" });
            }
        }

    }
}
