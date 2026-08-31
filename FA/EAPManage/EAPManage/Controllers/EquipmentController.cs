using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EquipmentController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId, string eqpType)
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
            foreach (var eqp in equipments)
            {
                var ports = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqp.EqpId);
                eqp.portInfos = ports;
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", equipments.Count);
            resdata.Add("list", equipments);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        [Route("GetDashboardSnapshot")]
        public IActionResult GetDashboardSnapshot(string eqpId, string eqpType)
        {
            try
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

                foreach (var eqp in equipments)
                {
                    eqp.portInfos = DataFactory.Current.PortInfo.GetList(u => u.eqpId == eqp.EqpId);
                }

                var activeAlarms = DataFactory.Current.ExceptionRecord.GetList(u => u.state == 0);
                if (!string.IsNullOrEmpty(eqpId))
                {
                    activeAlarms = activeAlarms.Where(a => a.eqpId == eqpId).ToList();
                }
                if (!string.IsNullOrEmpty(eqpType))
                {
                    activeAlarms = activeAlarms.Where(a => a.eqpType == eqpType).ToList();
                }

                var loopProcessStates = equipments
                    .Where(e => e.portInfos != null)
                    .SelectMany(e => e.portInfos.Select(p => new
                    {
                        eqpId = p.eqpId,
                        portId = p.portId,
                        loopProcessState = p.loopProcessState
                    }))
                    .ToList();

                return Ok(new
                {
                    Return = "OK",
                    MESSAGE = "SUCCESS",
                    data = new
                    {
                        equipments = new { total = equipments.Count, list = equipments },
                        activeAlarms = new { total = activeAlarms.Count, list = activeAlarms },
                        loopProcessStates,
                        serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
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
                    if (batch != null && batch.Count > 0)
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

        #region 循环跑片控制（按LP级别，持久化到数据库）

        /// <summary>
        /// 获取所有设备所有LP的循环跑片控制状态（从数据库读取并同步到AppCache）
        /// </summary>
        [HttpGet]
        [Route("GetLoopProcessState")]
        public IActionResult GetLoopProcessState()
        {
            try
            {
                // 从数据库读取所有PortInfo中的循环状态
                var ports = DataFactory.Current.PortInfo.GetAll();

                // 同步到 AppCache
                if (AppCache.Current.LoopProcessState == null)
                {
                    AppCache.Current.LoopProcessState = new Dictionary<string, Dictionary<int, int>>();
                }
                AppCache.Current.LoopProcessState.Clear();

                var result = new List<object>();
                var eqpGroups = ports.GroupBy(p => p.eqpId);
                foreach (var group in eqpGroups)
                {
                    var eqpId = group.Key;
                    if (!AppCache.Current.LoopProcessState.ContainsKey(eqpId))
                    {
                        AppCache.Current.LoopProcessState[eqpId] = new Dictionary<int, int>();
                    }
                    foreach (var port in group)
                    {
                        var state = port.loopProcessState;
                        AppCache.Current.LoopProcessState[eqpId][port.portId] = state;
                        result.Add(new { eqpId = port.eqpId, portId = port.portId, loopProcessState = state });
                    }
                }

                return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = result });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        /// <summary>
        /// 设置单个LP的循环跑片状态（写入数据库并更新AppCache）
        /// </summary>
        /// <param name="request">{ eqpId: string, portId: number, state: number }</param>
        [HttpPost]
        [Route("SetLoopProcessState")]
        public IActionResult SetLoopProcessState([FromBody] System.Text.Json.JsonElement requestBody)
        {
            try
            {
                var eqpId = requestBody.TryGetProperty("eqpId", out var eqpEl) ? eqpEl.GetString() : null;
                var portId = requestBody.TryGetProperty("portId", out var portEl) ? portEl.GetInt32() : 0;
                var state = requestBody.TryGetProperty("state", out var stateEl) ? stateEl.GetInt32() : -1;

                if (string.IsNullOrEmpty(eqpId))
                {
                    return Ok(new { Return = "NG", MESSAGE = "eqpId is required" });
                }

                if (state < 0 || state > 2)
                {
                    return Ok(new { Return = "NG", MESSAGE = "state must be 0(RUNNING), 1(STOPPED) or 2(PAUSED)" });
                }

                // 写入数据库
                var affected = DataFactory.Current.PortInfo.Update(
                    u => u.eqpId == eqpId && u.portId == portId,
                    u => new PortInfo { loopProcessState = state }
                );
                if (affected <= 0)
                {
                    return Ok(new { Return = "NG", MESSAGE = "Port not found" });
                }

                // 更新 AppCache
                if (AppCache.Current.LoopProcessState == null)
                {
                    AppCache.Current.LoopProcessState = new Dictionary<string, Dictionary<int, int>>();
                }
                if (!AppCache.Current.LoopProcessState.ContainsKey(eqpId))
                {
                    AppCache.Current.LoopProcessState[eqpId] = new Dictionary<int, int>();
                }
                AppCache.Current.LoopProcessState[eqpId][portId] = state;

                SendLoopProcessStateToMQ(eqpId, portId, state);

                var stateText = state == 0 ? "RUNNING" : state == 1 ? "STOPPED" : "PAUSED";
                Console.WriteLine($"[LoopProcess] eqp={eqpId} port={portId} state change to {stateText}");

                return Ok(new { Return = "OK", MESSAGE = $"LP {portId} loop process set to {stateText}", loopProcessState = state });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoopProcess ERROR] {ex.Message}");
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        /// <summary>
        /// 批量设置所有设备所有LP的循环跑片状态（写入数据库并更新AppCache）
        /// </summary>
        [HttpPost]
        [Route("SetAllLoopProcessState")]
        public IActionResult SetAllLoopProcessState([FromBody] Dictionary<string, object> request)
        {
            try
            {
                var state = Convert.ToInt32(request["state"]);

                if (state < 0 || state > 2)
                {
                    return Ok(new { Return = "NG", MESSAGE = "state must be 0(RUNNING), 1(STOPPED) or 2(PAUSED)" });
                }

                // 批量更新数据库
                var allPorts = DataFactory.Current.PortInfo.GetAll();
                foreach (var port in allPorts)
                {
                    DataFactory.Current.PortInfo.Update(
                        u => u.eqpId == port.eqpId && u.portId == port.portId,
                        u => new PortInfo { loopProcessState = state }
                    );
                }

                // 同步 AppCache
                if (AppCache.Current.LoopProcessState == null)
                {
                    AppCache.Current.LoopProcessState = new Dictionary<string, Dictionary<int, int>>();
                }
                AppCache.Current.LoopProcessState.Clear();

                var eqpGroups = allPorts.GroupBy(p => p.eqpId);
                foreach (var group in eqpGroups)
                {
                    var eqpId = group.Key;
                    AppCache.Current.LoopProcessState[eqpId] = new Dictionary<int, int>();
                    foreach (var port in group)
                    {
                        AppCache.Current.LoopProcessState[eqpId][port.portId] = state;
                    }
                }

                SendLoopProcessStateToMQ("*", 0, state);

                var stateText = state == 0 ? "RUNNING" : state == 1 ? "STOPPED" : "PAUSED";
                Console.WriteLine($"[LoopProcess] All eqp LP state change to {stateText}");

                return Ok(new { Return = "OK", MESSAGE = $"All LPs loop process set to {stateText}" });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        #endregion

        private void SendLoopProcessStateToMQ(string eqpId, int portId, int state)
        {
            try
            {
                var xmlMessage = $@"<Request>
                        <Header>
                        <MESSAGENAME>LOOPPROCESSSTATE</MESSAGENAME>
                        </Header>
                        <Body>
                        <EQUIPMENTID>{eqpId}</EQUIPMENTID>
                        <PORTID>{portId}</PORTID>
                        <STATE>{state}</STATE>
                        </Body>
                    </Request>";
                EAPManage.Utils.ActiveMQClient.Current.SendAsync(xmlMessage);
                Console.WriteLine($"[LoopProcess MQ Sent] eqp={eqpId} port={portId} state={state}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoopProcess MQ Send Error] {ex.Message}");
            }
        }
    }
}
