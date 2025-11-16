using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Linq;


namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LotInfoController : ControllerBase
    {
        [HttpGet]
        [Route("{LotId}")]
        public IActionResult Get(string LotId)
        {
            List<LotInfo> lotInfos = DataFactory.Current.Lot.GetList(u => u.LotId == LotId);
            foreach(var lot in lotInfos)
            {
                var wafers= DataFactory.Current.Wafer.GetList(u => u.lotId == lot.LotId);
                lot.Slots = wafers.Select(p => p.WaferId).ToList();
                lot.SlotsStr = string.Join(", ", wafers.Select(p => p.Slot));
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", lotInfos.Count);
            resdata.Add("list", lotInfos);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        [Route("Idle")]
        public IActionResult GetIdle()
        {
            List<LotInfo> lotInfos = DataFactory.Current.Lot.GetList(u => string.IsNullOrEmpty(u.BatchId));
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", lotInfos.Count);
            resdata.Add("list", lotInfos.Select(p=>p.LotId).ToList());
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        public IActionResult Get()
        {
            List<LotInfo> lotInfos = DataFactory.Current.Lot.GetAll();
              foreach(var lot in lotInfos)
            {
                var wafers= DataFactory.Current.Wafer.GetList(u => u.lotId == lot.LotId);
                lot.Slots = wafers.Select(p => p.WaferId).ToList();
                lot.SlotsStr = string.Join(", ", wafers.Select(p => p.Slot));
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", lotInfos.Count);
            resdata.Add("list", lotInfos);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        [Route("GetPPIDList/{LotId}")]
        public IActionResult GetPPIDList(string LotId)
        {
            List<LotInfo> lotInfos = DataFactory.Current.Lot.GetList(u => u.LotId == LotId);
            if (string.IsNullOrEmpty(lotInfos[0]?.BatchId))
            {
                Dictionary<string, object> resdata = new Dictionary<string, object>();
                resdata.Add("list", new List<string>());
                return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
            }
            else
            {
                var eqpId=  DataFactory.Current.Batch.GetList(u => u.BatchId == lotInfos[0].BatchId)?[0].EqpId;
                var pplist= EAPClient.Get_PPID_List(eqpId);
                Dictionary<string, object> resdata = new Dictionary<string, object>();
                resdata.Add("list", pplist);
                return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
            }
        }

        [HttpPost]
        public IActionResult Post([FromBody] LotInfo lotInfo)
        {
            try
            {
                var res = false;
                var res1 = 0;
                if (lotInfo.Slots.Count != lotInfo.QTY)
                {
                    return Ok(new { Return = "NG", MESSAGE = "Slot 数量和QTY 不一致" });
                }
                // 事务操作
                DataFactory.Current.Lot.ExecuteTransaction(db =>
                {
                     res = DataFactory.Current.Lot.Insert(lotInfo);
                    List<Wafer> wafers = new List<Wafer>();
                    foreach(var slot in lotInfo.Slots)
                    {
                        Wafer wafer = new Wafer()
                        {
                            lotId = lotInfo.LotId,
                            WaferId = slot,
                            Slot = Int32.Parse(slot.Substring(slot.Length - 2, 2))
                        };
                        wafers.Add(wafer);
                    }
                    res1= DataFactory.Current.Wafer.Insert(wafers);
                });
                if (res && res1!=0)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
                }
            }
            catch(Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] LotInfo lotInfo)
        {
            try
            {
                var res = false;
                var res1 = 0;
                if (lotInfo.Slots.Count != lotInfo.QTY)
                {
                    return Ok(new { Return = "NG", MESSAGE = "Slot 数量和QTY 不一致" });
                }
                // 事务操作
                DataFactory.Current.Lot.ExecuteTransaction(db =>
                {
                    res = DataFactory.Current.Lot.Update(lotInfo);
                    DataFactory.Current.Wafer.Delete(u => u.lotId == lotInfo.LotId);
                    List<Wafer> wafers = new List<Wafer>();
                    foreach (var slot in lotInfo.Slots)
                    {
                        Wafer wafer = new Wafer()
                        {
                            lotId = lotInfo.LotId,
                            WaferId = slot,
                            Slot = Int32.Parse(slot.Substring(slot.Length - 2, 2))
                        };
                        wafers.Add(wafer);
                    }
                    res1 = DataFactory.Current.Wafer.Insert(wafers);

                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新Lot失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "更新Lot失败" });
            }
        }

        [HttpDelete]
        public IActionResult Delete([FromBody] LotInfo lotInfo)
        {
            try
            {
                var res = false;
                if(!string.IsNullOrEmpty(lotInfo.BatchId))
                    return Ok(new { Return = "NG", MESSAGE = "删除LOT失败" });
                // 事务操作
                DataFactory.Current.Lot.ExecuteTransaction(db =>
                {
                    res = DataFactory.Current.Lot.DeleteById(lotInfo.Id);
                    DataFactory.Current.Wafer.Delete(u => u.lotId == lotInfo.LotId);
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除LOT失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除LOT失败" });
            }
        }

        [HttpDelete]
        [Route("DeleteLots")]
        public IActionResult DeleteLots([FromBody] List<int> Ids)
        {
            try
            {
                var res = true;
                if (Ids.Count > 0)
                {
                    foreach (var Id in Ids)
                    {
                        var lot = DataFactory.Current.Lot.GetById(Id);
                        if (string.IsNullOrEmpty(lot.BatchId))
                            return Ok(new { Return = "NG", MESSAGE = "删除LOT失败" });
                    }

                    DataFactory.Current.Lot.ExecuteTransaction(db =>
                    {
                        foreach (var Id in Ids)
                        {
                            var lot = DataFactory.Current.Lot.GetById(Id);
                            res = DataFactory.Current.Lot.DeleteById(Id);
                            DataFactory.Current.Wafer.Delete(u => u.lotId == lot.LotId);
                        }
                    });
                }
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "批量删除LOT失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "批量删除LOT失败" });
            }
        }
    }
}
