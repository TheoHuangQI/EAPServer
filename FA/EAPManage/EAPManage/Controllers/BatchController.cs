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

            // 补充查询关联的 Lots
            foreach (var batch in batches)
            {
                var relatedLots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                batch.lots = relatedLots.Select(u => u.LotId).ToList();
            }

            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", batches.Count);
            resdata.Add("list", batches);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        public IActionResult Get()
        {
            List<Batch> batches = DataFactory.Current.Batch.GetAll();

            // 补充查询每个 Batch 关联的 Lots，防止前端编辑时 lots 为空
            foreach (var batch in batches)
            {
                var relatedLots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                batch.lots = relatedLots.Select(u => u.LotId).ToList();
            }

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
                foreach (var lot in batch.lots)
                {
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
                batch.BatchSize = batch.lots.Count;
                batch.TID = Guid.NewGuid().ToString();
                batch.BatchId = DateTime.Now.ToString("yyyyMMddHHmmssffff") + new Random().Next(1000, 9999).ToString();
                // 事务操作
                DataFactory.Current.Batch.ExecuteTransaction(db =>
                {

                    foreach (var lot in batch.lots)
                    {
                        DataFactory.Current.Lot.Update(u => u.LotId == lot, u => new LotInfo
                        {
                            BatchId = batch.BatchId,
                            LotState = LotState.WaitTrackIn
                        });
                    }

                    DataFactory.Current.Carrier.Update(u => u.CarrierId == batch.CarrierId, u => new Carrier
                    {
                        In_use = CarrierUseState.USE
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
                // 确保 lots 不为 null，防止遍历时报错
                if (batch.lots == null)
                {
                    batch.lots = new List<string>();
                }

                List<int> slots = new List<int>();
                foreach (var lot in batch.lots)
                {
                    var lotInfo = DataFactory.Current.Lot.GetList(u => u.LotId == lot).FirstOrDefault();
                    //更新绑定的Lot 已经被其他Batch绑定
                    if(!string.IsNullOrEmpty(lotInfo.BatchId) && lotInfo.BatchId!=batch.BatchId)
                        return Ok(new { Return = "NG", MESSAGE = "更新Batch失败：选择的Lot已被其他Batch绑定" });

                    // 允许的状态：空闲(Idle) 或者 已经是当前Batch分配的(WaitTrackIn)
                    // 如果正在加工(TrackIn)，只允许保留，不允许新加处于 TrackIn 的 Lot，但这里因为是遍历前端传来的所有 lot，
                    // 所以只要它是属于当前 Batch 且处于 TrackIn，也应该放行，否则无法完成修改（比如删掉其他的）
                    if (lotInfo.LotState != LotState.Idle && 
                        lotInfo.LotState != LotState.WaitTrackIn && 
                        !(lotInfo.BatchId == batch.BatchId && lotInfo.LotState == LotState.TrackIn))
                        return Ok(new { Return = "NG", MESSAGE = $"更新Batch失败：Lot状态不正确 ({lotInfo.LotState})" });

                    List<Wafer> wafers = DataFactory.Current.Wafer.GetList(u => u.lotId == lot);
                    foreach (var wafer in wafers)
                    {
                        if (slots.Contains(wafer.Slot))
                        {
                            //return Ok(new { Return = "NG", MESSAGE = $"选择的Lot中有重复的Slot:{wafer.Slot}" });
                        }
                        slots.Add(wafer.Slot);
                    }
                }

                var carrierInfo = DataFactory.Current.Carrier.GetList(u => u.CarrierId == batch.CarrierId).FirstOrDefault();
                // 检查载具状态，如果是编辑操作，且选择的载具就是当前Batch正在使用的载具，则不报错
                if (carrierInfo.In_use != CarrierUseState.IDLE)
                {
                    // 需要获取数据库里原本的 batch 记录，看看它原来的载具是不是现在这个
                    var oldBatch = DataFactory.Current.Batch.GetById(batch.Id);
                    if (oldBatch == null || oldBatch.CarrierId != batch.CarrierId)
                    {
                        //return Ok(new { Return = "NG", MESSAGE = "更新Batch失败：该载具已被占用" });
                    }
                }

                var res = false;
                DataFactory.Current.Batch.ExecuteTransaction(db =>
                {
                    var lots = DataFactory.Current.Lot.GetList(u => u.BatchId == batch.BatchId);
                    foreach (var lot in lots)
                    {
                        if (!batch.lots.Contains(lot.LotId))
                        {
                            lot.BatchId = string.Empty;
                            lot.LotState = LotState.Idle;
                            DataFactory.Current.Lot.Update(lot);
                        }
                    }
                    var lotIds = lots.Select(u => u.LotId);
                    foreach(var lot in batch.lots)
                    {
                        if (!lotIds.Contains(lot))
                        {
                            var lotInfo = DataFactory.Current.Lot.GetList(u => u.LotId == lot).FirstOrDefault();
                            lotInfo.BatchId = batch.BatchId;
                            // 只有原本是 Idle 的才改为 WaitTrackIn，如果是已经在加工的不应该随便改状态
                            if (lotInfo.LotState == LotState.Idle)
                            {
                                lotInfo.LotState = LotState.WaitTrackIn;
                            }
                            DataFactory.Current.Lot.Update(lotInfo);
                        }
                    }

                    // 更新批次大小
                    batch.BatchSize = batch.lots.Count;

                    // 如果载具发生了变化，需要把旧载具释放
                    var oldBatch = DataFactory.Current.Batch.GetById(batch.Id);
                    if (oldBatch != null && oldBatch.CarrierId != batch.CarrierId)
                    {
                        DataFactory.Current.Carrier.Update(u => u.CarrierId == oldBatch.CarrierId, u => new Carrier
                        {
                            In_use = CarrierUseState.IDLE
                        });
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
                    if (lot.LotState != LotState.WaitTrackIn)
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
