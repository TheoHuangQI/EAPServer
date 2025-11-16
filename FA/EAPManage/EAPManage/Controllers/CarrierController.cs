using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace EAPManage.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class CarrierController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string carrierId,string in_use,int pageNo,int pageSize)
        {
            PageResult<Carrier> carriers = DataFactory.Current.Carrier.GetPageList(u =>
            carrierId != null ? u.CarrierId == carrierId : true
            && in_use == null ? true : u.In_use == (CarrierUseState)(int.Parse(in_use)), pageNo, pageSize);
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", carriers.TotalCount);
            resdata.Add("list", carriers.Data);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }


        [HttpGet]
        [Route("Idle")]
        public IActionResult GetIdle()
        {
            List<Carrier> carriers = DataFactory.Current.Carrier.GetList(u => u.In_use==CarrierUseState.IDLE);
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", carriers.Count);
            resdata.Add("list", carriers.Select(p => p.CarrierId).ToList());
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPost]
        public IActionResult Post([FromBody] Carrier carrier)
        {
            try
            {
                var res = DataFactory.Current.Carrier.Insert(carrier);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加载具失败" });
                }
            }
            catch 
            {
                return Ok(new { Return = "NG", MESSAGE = "添加载具失败" });
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] Carrier carrier)
        {
            try
            {
                var batch = DataFactory.Current.Batch.GetList(u => u.CarrierId == carrier.CarrierId);
                if(batch!=null && batch.Count > 0)
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新载具失败" });
                }

                var res = DataFactory.Current.Carrier.Update(carrier);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新载具失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "更新载具失败" });
            }
        }

        [HttpDelete]
        public IActionResult Delete([FromBody] List<int> Ids)
        {
            try
            {
                var res = true;
                if (Ids.Count > 0)
                {
                    foreach (var Id in Ids)
                    {
                        var carrierId = DataFactory.Current.Carrier.GetById(Id).CarrierId;
                        var batch = DataFactory.Current.Batch.GetList(u => u.CarrierId == carrierId);
                        if (batch != null && batch.Count > 0)
                        {
                            return Ok(new { Return = "NG", MESSAGE = "删除载具失败" });
                        }
                    }

                    DataFactory.Current.Carrier.ExecuteTransaction(db =>
                    {
                        foreach (var Id in Ids)
                        {
                            var res = DataFactory.Current.Carrier.DeleteById(Id);
                        }
                    });
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "批量删除载具失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "批量删除载具失败" });
            }
        }
    }
}
