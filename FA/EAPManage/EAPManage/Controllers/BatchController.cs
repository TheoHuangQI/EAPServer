using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BatchController : ControllerBase
    {
        [HttpGet]
        [Route("{batchId}")]
        public IActionResult Get(string batchId)
        {
            List<Batch> batches = DataFactory.Current.Batch.GetList(u => u.BatchId == batchId);
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", batches.Count);
            resdata.Add("list", batches);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        public IActionResult Get()
        {
            List<Batch> batches = DataFactory.Current.Batch.GetAll();
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", batches.Count);
            resdata.Add("list", batches);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPost]
        public IActionResult Post([FromBody] Batch batch)
        {
            try
            {
                var res = false;
                List<int> slots = new List<int>();
                foreach(var lot in batch.lots)
                {
                    List<Wafer> wafers=DataFactory.Current.Wafer.GetList(u => u.lotId ==lot);
                    foreach(var wafer in wafers)
                    {
                        if (slots.Contains(wafer.Slot))
                        {
                            return Ok(new { Return = "NG", MESSAGE = $"选择的Lot中有重复的Slot:{wafer.Slot}" });
                        }
                        slots.Add(wafer.Slot);
                    }
                }
                batch.BatchSize = batch.lots.Count;
                batch.TID= Guid.NewGuid().ToString();
                batch.BatchId= DateTime.Now.ToString("yyyyMMddHHmmssffff") + new Random().Next(1000, 9999).ToString();
                // 事务操作
                DataFactory.Current.Batch.ExecuteTransaction(db =>
                {
                    
                    foreach (var lot in batch.lots)
                    {
                        DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                        {
                           BatchId=batch.BatchId,
                           LotState=LotState.WaitTrackIn
                        });
                    }

                    DataFactory.Current.Carrier.Update(u => u.CarrierId == batch.CarrierId, u => new Carrier
                    {
                        In_use =CarrierUseState.USE
                    });
                    res = DataFactory.Current.Batch.Insert(batch);
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加Batch失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "添加Batch失败" });
            }
        }

        [HttpPut]
        public IActionResult Put([FromBody] Batch batch)
        {
            try
            {
                List<int> slots = new List<int>();
                foreach (var lot in batch.lots)
                {
                    var lotInfo = DataFactory.Current.Lot.GetList(u => u.LotId == lot).FirstOrDefault();
                    //更新绑定的Lot 已经被其他Batch绑定
                    if(!string.IsNullOrEmpty(lotInfo.BatchId) && lotInfo.BatchId!=batch.BatchId)
                        return Ok(new { Return = "NG", MESSAGE = "更新Batch失败" });

                    if (lotInfo.LotState != LotState.WaitTrackIn)
                        return Ok(new { Return = "NG", MESSAGE = "更新Batch失败" });

                    List<Wafer> wafers = DataFactory.Current.Wafer.GetList(u => u.lotId == lot);
                    foreach (var wafer in wafers)
                    {
                        if (slots.Contains(wafer.Slot))
                        {
                            return Ok(new { Return = "NG", MESSAGE = $"选择的Lot中有重复的Slot:{wafer.Slot}" });
                        }
                        slots.Add(wafer.Slot);
                    }
                }

                var carrierInfo = DataFactory.Current.Carrier.GetList(u => u.CarrierId == batch.CarrierId).FirstOrDefault();
                if(carrierInfo.In_use!=CarrierUseState.IDLE)
                    return Ok(new { Return = "NG", MESSAGE = "更新Batch失败" });

                var res = false;
                DataFactory.Current.Batch.ExecuteTransaction(db =>
                {
                    var lots = DataFactory.Current.Lot.GetList(u=>u.BatchId==batch.BatchId);
                    foreach(var lot in lots)
                    {
                        if (!batch.lots.Contains(lot.LotId))
                        {
                            lot.BatchId = string.Empty;
                            lot.LotState = LotState.Idle;
                            res = DataFactory.Current.Lot.Update(lot);
                        }
                    }
                    var lotIds = lots.Select(u => u.LotId);
                    foreach(var lot in batch.lots)
                    {
                        if (!lotIds.Contains(lot))
                        {
                            var lotInfo = DataFactory.Current.Lot.GetList(u => u.LotId == lot).FirstOrDefault();
                            lotInfo.BatchId = batch.BatchId;
                            lotInfo.LotState = LotState.WaitTrackIn;
                            res = DataFactory.Current.Lot.Update(lotInfo);
                        }
                    }

                    DataFactory.Current.Carrier.Update(u => u.CarrierId == batch.CarrierId, u => new Carrier
                    {
                        In_use = CarrierUseState.USE
                    });
                    res = DataFactory.Current.Batch.Update(batch);
                });
                    
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "更新Batch失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "更新Batch失败" });
            }
        }

        [HttpDelete]
        public IActionResult Delete([FromBody] Batch batch)
        {
            try
            {
                var lots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                var res = false;
                foreach (var lot in lots)
                {
                    if(lot.LotState!=LotState.WaitTrackIn)
                        return Ok(new { Return = "NG", MESSAGE = "删除Batch失败" });
                }

                DataFactory.Current.Batch.ExecuteTransaction(db =>
                {
                    foreach (var lot in lots)
                    {
                        lot.BatchId = string.Empty;
                        lot.LotState = LotState.Idle;
                        res = DataFactory.Current.Lot.Update(lot);
                    }
                    DataFactory.Current.Carrier.Update(u => u.CarrierId == batch.CarrierId, u => new Carrier
                    {
                        In_use = CarrierUseState.IDLE
                    });
                    res = DataFactory.Current.Batch.DeleteById(batch.Id);
                });
             
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除Batch失败" });
                }
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除Batch失败" });
            }
        }

        [HttpDelete]
        [Route("DeleteBatchs")]
        public IActionResult DeleteBatchs([FromBody] List<int> batchIds)
        {
            try
            {
                var res = false;
                if (batchIds.Count > 0)
                {
                    foreach (var batchId in batchIds)
                    {
                        var batch = DataFactory.Current.Batch.GetById(batchId);
                        var lots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                        foreach (var lot in lots)
                        {
                            if (lot.LotState != LotState.WaitTrackIn)
                            {
                                return Ok(new { Return = "NG", MESSAGE = "删除Batch失败" });
                            }

                        }
                    }

                    DataFactory.Current.Batch.ExecuteTransaction(db =>
                    {
                        foreach (var batchId in batchIds)
                        {
                            var batch = DataFactory.Current.Batch.GetById(batchId);
                            var lots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                            foreach (var lot in lots)
                            {
                                lot.BatchId = string.Empty;
                                lot.LotState = LotState.Idle;
                                res = DataFactory.Current.Lot.Update(lot);
                            }

                            DataFactory.Current.Carrier.Update(u => u.CarrierId == batch.CarrierId, u => new Carrier
                            {
                                In_use = CarrierUseState.IDLE
                            });
                            res = DataFactory.Current.Batch.DeleteById(batch.Id);
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
