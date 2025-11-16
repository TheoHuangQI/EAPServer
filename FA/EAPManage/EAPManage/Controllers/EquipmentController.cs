using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EquipmentController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId,string eqpType)
        {
            List<Equipment> equipments = DataFactory.Current.Equipment.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                equipments = equipments.Where(a => a.EqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(eqpType))
            {
                equipments = equipments.Where(a => a.EqpType == eqpType).ToList();
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", equipments.Count);
            resdata.Add("list", equipments);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        [Route("GetEqpInfo")]
        public IActionResult GetEqpInfo(string eqpId, string eqpType)
        {
            List<Equipment> equipments = DataFactory.Current.Equipment.GetAll();
            if (!string.IsNullOrEmpty(eqpId))
            {
                equipments = equipments.Where(a => a.EqpId == eqpId).ToList();
            }
            if (!string.IsNullOrEmpty(eqpType))
            {
                equipments = equipments.Where(a => a.EqpType == eqpType).ToList();
            }
            foreach(var eqp in equipments)
            {
                var ports = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqp.EqpId);
                eqp.portInfos = ports;
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", equipments.Count);
            resdata.Add("list", equipments);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPost]
        public IActionResult Post([FromBody] Equipment equipment)
        {
            try
            {
                var res = DataFactory.Current.Equipment.Insert(equipment);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加设备失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "添加设备失败" });
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] Equipment equipment)
        {
            try
            {
                if (equipment.Enable == 0)
                {
                    var batch = DataFactory.Current.Batch.GetList(u => u.EqpId == equipment.EqpId);
                    if(batch!=null && batch.Count > 0)
                    {
                        return Ok(new { Return = "NG", MESSAGE = "更新设备失败" });
                    }
                }
                var res = DataFactory.Current.Equipment.Update(equipment);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新设备失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "更新设备失败" });
            }
        }


        [HttpDelete]
        public IActionResult Delete([FromBody] Equipment equipment)
        {
            try
            {
                var batch = DataFactory.Current.Batch.GetList(u => u.EqpId == equipment.EqpId);
                if (batch != null && batch.Count > 0)
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新设备失败" });
                }

                var res = DataFactory.Current.Equipment.DeleteById(equipment.Id);
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

        [HttpDelete]
        [Route("Deletes")]
        public IActionResult Deletes([FromBody] List<int> Ids)
        {
            try
            {
                var res = true;
                if (Ids.Count > 0)
                {
                    foreach (var Id in Ids)
                    {
                        var equipment = DataFactory.Current.Equipment.GetById(Id);
                        var batch = DataFactory.Current.Batch.GetList(u => u.EqpId == equipment.EqpId);
                        if (batch != null && batch.Count > 0)
                        {
                            return Ok(new { Return = "NG", MESSAGE = "批量删除设备失败" });
                        }
                    }

                    DataFactory.Current.Carrier.ExecuteTransaction(db =>
                    {
                        foreach (var Id in Ids)
                        {
                            var res = DataFactory.Current.Equipment.DeleteById(Id);
                        }
                    });
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "批量删除设备失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "批量删除设备失败" });
            }
        }
    }
}
