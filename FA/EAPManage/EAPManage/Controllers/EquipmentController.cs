using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.Json;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EquipmentController : ControllerBase
    {
        private static readonly object ConnectionConfigLock = new object();

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
        [Route("GetConnectionConfig")]
        public IActionResult GetConnectionConfig(string eqpId)
        {
            if (string.IsNullOrWhiteSpace(eqpId))
                return Ok(new { Return = "NG", MESSAGE = "eqpId is required" });

            try
            {
                lock (ConnectionConfigLock)
                {
                    var files = FindConnectionConfigPaths()
                        .Select(path => (Path: path, Configs: LoadConnectionConfigs(path)))
                        .ToList();
                    var config = files.SelectMany(file => file.Configs.OfType<JObject>()).FirstOrDefault(item =>
                        string.Equals((string)item["Name"], eqpId, StringComparison.OrdinalIgnoreCase));

                    if (config == null)
                    {
                        var equipment = DataFactory.Current.Equipment.GetAll().FirstOrDefault(item =>
                            string.Equals(item.EqpId, eqpId, StringComparison.OrdinalIgnoreCase));
                        if (equipment == null)
                            return Ok(new { Return = "NG", MESSAGE = $"Configuration for {eqpId} was not found" });

                        config = CreateDefaultConnectionConfig(equipment);
                    }

                    var filesToUpdate = files.Where(file => !file.Configs.OfType<JObject>().Any(item =>
                        string.Equals((string)item["Name"], eqpId, StringComparison.OrdinalIgnoreCase))).ToList();
                    foreach (var file in filesToUpdate)
                        file.Configs.Add((JObject)config.DeepClone());
                    if (filesToUpdate.Count > 0)
                        WriteConnectionConfigFiles(filesToUpdate.Select(file => (file.Path, file.Configs.ToString(Formatting.Indented))).ToList());

                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = ToJsonElement(config) });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        private static JObject CreateDefaultConnectionConfig(Equipment equipment)
        {
            return new JObject
            {
                ["Name"] = equipment.EqpId,
                ["DeviceID"] = 0,
                ["ConnectionMode"] = "ACTIVE",
                ["LocalIP"] = "0.0.0.0",
                ["LocalPort"] = 5000,
                ["RemoteIP"] = "0.0.0.0",
                ["RemotePort"] = 5000,
                ["EQPType"] = equipment.EqpType,
                ["T3"] = 45,
                ["T5"] = 10,
                ["T6"] = 5,
                ["T7"] = 10,
                ["T8"] = 5,
                ["ConnectionType"] = "HSMS",
                ["Chamber"] = "9,10,11,13,14,15,17,18,19,21,22,23",
                ["LoadPort"] = 4,
                ["EventReportFile"] = "event-report-CH.xml",
                ["HeartbeatInterval"] = 30,
                ["HeartbeatMaxFailures"] = 3,
                ["AutoReconnect"] = true,
                ["Enabled"] = true
            };
        }

        private void SetConnectionConfigEnabled(string eqpId, bool enabled)
        {
            lock (ConnectionConfigLock)
            {
                var files = FindConnectionConfigPaths()
                    .Select(path => (Path: path, Configs: LoadConnectionConfigs(path)))
                    .ToList();
                var pendingWrites = new List<(string Path, string Content)>();
                foreach (var file in files)
                {
                    var config = file.Configs.OfType<JObject>().FirstOrDefault(item =>
                        string.Equals((string)item["Name"], eqpId, StringComparison.OrdinalIgnoreCase));
                    if (config == null)
                        throw new InvalidOperationException($"Configuration for {eqpId} was not found in {file.Path}");

                    config["Enabled"] = enabled;
                    pendingWrites.Add((file.Path, file.Configs.ToString(Formatting.Indented)));
                }

                WriteConnectionConfigFiles(pendingWrites);
            }
        }

        private static void WriteConnectionConfigFiles(List<(string Path, string Content)> files)
        {
            var snapshots = files.ToDictionary(file => file.Path, file => System.IO.File.ReadAllBytes(file.Path), StringComparer.OrdinalIgnoreCase);
            var tempFiles = new List<(string TempPath, string TargetPath)>();
            var replacedPaths = new List<string>();
            try
            {
                foreach (var file in files)
                {
                    var tempPath = file.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    tempFiles.Add((tempPath, file.Path));
                    System.IO.File.WriteAllText(tempPath, file.Content);
                }
                foreach (var tempFile in tempFiles)
                {
                    System.IO.File.Move(tempFile.TempPath, tempFile.TargetPath, true);
                    replacedPaths.Add(tempFile.TargetPath);
                }
            }
            catch (Exception writeError)
            {
                var rollbackErrors = new List<Exception>();
                foreach (var path in replacedPaths)
                {
                    try
                    {
                        System.IO.File.WriteAllBytes(path, snapshots[path]);
                    }
                    catch (Exception rollbackError)
                    {
                        rollbackErrors.Add(rollbackError);
                    }
                }
                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, writeError);
                    throw new AggregateException("Configuration write failed and rollback could not restore all files.", rollbackErrors);
                }
                throw;
            }
            finally
            {
                foreach (var tempFile in tempFiles)
                    if (System.IO.File.Exists(tempFile.TempPath)) System.IO.File.Delete(tempFile.TempPath);
            }
        }

        [HttpPut]
        [Route("SetConnectionConfig")]
        public IActionResult SetConnectionConfig([FromBody] JsonElement requestBody)
        {
            if (requestBody.ValueKind != JsonValueKind.Object)
                return Ok(new { Return = "NG", MESSAGE = "A JSON object is required" });

            var request = JObject.Parse(requestBody.GetRawText());
            var eqpId = (string)request?["eqpId"];
            var config = request?["config"] as JObject;
            if (string.IsNullOrWhiteSpace(eqpId) || config == null)
                return Ok(new { Return = "NG", MESSAGE = "eqpId and config are required" });

            var validationError = ValidateConnectionConfig(eqpId, config);
            if (validationError != null)
                return Ok(new { Return = "NG", MESSAGE = validationError });

            try
            {
                List<string> savedPaths;
                lock (ConnectionConfigLock)
                {
                    var paths = FindConnectionConfigPaths();
                    var pendingWrites = new List<(string Path, string Content)>();
                    JObject savedConfig = null;
                    foreach (var path in paths)
                    {
                        var configs = LoadConnectionConfigs(path);
                        var index = configs.Select((item, position) => new { item, position })
                            .FirstOrDefault(entry => entry.item is JObject item &&
                                string.Equals((string)item["Name"], eqpId, StringComparison.OrdinalIgnoreCase))?.position;
                        if (!index.HasValue)
                            return Ok(new { Return = "NG", MESSAGE = $"Configuration for {eqpId} was not found in {path}" });

                        var fileConfig = (JObject)configs[index.Value];
                        foreach (var property in config.Properties())
                        {
                            var savedProperty = fileConfig.Property(property.Name, StringComparison.OrdinalIgnoreCase);
                            if (savedProperty == null)
                                fileConfig.Add(property.Name, property.Value.DeepClone());
                            else
                                savedProperty.Value = property.Value.DeepClone();
                        }
                        fileConfig["Name"] = eqpId;
                        savedConfig ??= (JObject)fileConfig.DeepClone();
                        pendingWrites.Add((path, configs.ToString(Formatting.Indented)));
                    }

                    WriteConnectionConfigFiles(pendingWrites);
                    config = savedConfig;
                    savedPaths = paths.Select(Path.GetFullPath).ToList();
                }

                return Ok(new { Return = "OK", MESSAGE = "Configuration saved", data = ToJsonElement(config), files = savedPaths });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        private string FindConnectionConfigPath()
        {
            var configuredDirectory = HttpContext.RequestServices.GetService(typeof(IConfiguration)) is IConfiguration configuration
                ? configuration["EapConfigurationDirectory"]
                : null;
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
            {
                var configuredPath = Path.Combine(configuredDirectory, "AHEG07.json");
                if (System.IO.File.Exists(configuredPath)) return configuredPath;
            }

            var roots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
            var candidates = new List<string>();
            foreach (var root in roots)
            {
                var directory = new DirectoryInfo(root);
                while (directory != null)
                {
                    candidates.Add(Path.Combine(directory.FullName, "OutExe", "Configuration", "AHEG07.json"));
                    candidates.Add(Path.Combine(directory.FullName, "EAPServer", "Configuration", "AHEG07.json"));
                    candidates.Add(Path.Combine(directory.FullName, "Configuration", "AHEG07.json"));
                    directory = directory.Parent;
                }
            }

            var path = candidates.FirstOrDefault(System.IO.File.Exists);
            if (path == null)
                throw new FileNotFoundException("AHEG07.json was not found. Set EapConfigurationDirectory to its directory.");
            return path;
        }

        private List<string> FindConnectionConfigPaths()
        {
            var primaryPath = FindConnectionConfigPath();
            var configurationDirectory = new DirectoryInfo(Path.GetDirectoryName(primaryPath));
            var applicationDirectory = configurationDirectory.Parent;
            if (!string.Equals(configurationDirectory.Name, "Configuration", StringComparison.OrdinalIgnoreCase) ||
                applicationDirectory?.Parent == null ||
                (!string.Equals(applicationDirectory.Name, "OutExe", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(applicationDirectory.Name, "EAPServer", StringComparison.OrdinalIgnoreCase)))
            {
                return new List<string> { primaryPath };
            }

            var root = applicationDirectory.Parent.FullName;
            var paths = new List<string>
            {
                Path.Combine(root, "OutExe", "Configuration", "AHEG07.json"),
                Path.Combine(root, "EAPServer", "Configuration", "AHEG07.json")
            };
            paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var path in paths.Where(path => !System.IO.File.Exists(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                System.IO.File.Copy(primaryPath, path);
            }
            return paths;
        }

        private static JArray LoadConnectionConfigs(string path)
        {
            using var reader = new JsonTextReader(System.IO.File.OpenText(path));
            var token = JToken.ReadFrom(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Load });
            return token as JArray ?? throw new InvalidDataException("AHEG07.json must contain a JSON array");
        }

        private static Dictionary<string, byte[]> CaptureConnectionConfigFiles(IEnumerable<string> paths)
        {
            return paths.ToDictionary(path => path, path => System.IO.File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);
        }

        private static void RestoreConnectionConfigFiles(Dictionary<string, byte[]> snapshots)
        {
            foreach (var snapshot in snapshots)
                System.IO.File.WriteAllBytes(snapshot.Key, snapshot.Value);
        }

        private static JsonElement ToJsonElement(JToken token)
        {
            using var document = JsonDocument.Parse(token.ToString(Formatting.None));
            return document.RootElement.Clone();
        }

        private static string ValidateConnectionConfig(string eqpId, JObject config)
        {
            if (!string.Equals((string)config["Name"], eqpId, StringComparison.OrdinalIgnoreCase))
                return "Configuration name must match the device ID";
            var connectionMode = (string)config["ConnectionMode"];
            if (!string.Equals(connectionMode, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(connectionMode, "PASSIVE", StringComparison.OrdinalIgnoreCase))
                return "ConnectionMode must be ACTIVE or PASSIVE";
            var connectionType = (string)config["ConnectionType"];
            if (!string.Equals(connectionType, "SECSI", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(connectionType, "HSMS", StringComparison.OrdinalIgnoreCase))
                return "ConnectionType must be SECSI or HSMS";
            if (!IPAddress.TryParse((string)config["LocalIP"], out _))
                return "LocalIP is invalid";
            var isActive = string.Equals(connectionMode, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            var remoteIp = (string)config["RemoteIP"];
            if ((isActive || !string.IsNullOrWhiteSpace(remoteIp)) && !IPAddress.TryParse(remoteIp, out _))
                return "RemoteIP is invalid";

            foreach (var name in new[] { "LocalPort", "T3", "T5", "T6", "T7", "T8", "LoadPort", "HeartbeatInterval", "HeartbeatMaxFailures" })
            {
                if (!int.TryParse((string)config[name], out var value) || value <= 0)
                    return $"{name} must be a positive integer";
            }
            if (!int.TryParse((string)config["RemotePort"], out var remotePort) || remotePort < (isActive ? 1 : 0) || remotePort > 65535)
                return "RemotePort is invalid";
            if (int.Parse((string)config["LocalPort"]) > 65535 || remotePort > 65535)
                return "Port must not exceed 65535";
            if (!int.TryParse((string)config["DeviceID"], out var deviceId) || deviceId < 0 || deviceId > 65535)
                return "DeviceID must be between 0 and 65535";
            foreach (var name in new[] { "EQPType", "Chamber", "EventReportFile" })
            {
                if (string.IsNullOrWhiteSpace((string)config[name])) return $"{name} is required";
            }
            return null;
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
                if (equipment == null || string.IsNullOrWhiteSpace(equipment.EqpId) || string.IsNullOrWhiteSpace(equipment.EqpType))
                    return Ok(new { Return = "NG", MESSAGE = "设备号和设备类型不能为空" });

                equipment.EqpId = equipment.EqpId.Trim();
                equipment.EqpType = equipment.EqpType.Trim();

                lock (ConnectionConfigLock)
                {
                    if (DataFactory.Current.Equipment.GetAll().Any(item =>
                        string.Equals(item.EqpId, equipment.EqpId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Ok(new { Return = "NG", MESSAGE = $"设备 {equipment.EqpId} 已存在" });
                    }

                    var files = FindConnectionConfigPaths()
                        .Select(path => (Path: path, Configs: LoadConnectionConfigs(path)))
                        .ToList();
                    var originalFiles = CaptureConnectionConfigFiles(files.Select(file => file.Path));
                    var config = files.SelectMany(file => file.Configs.OfType<JObject>()).FirstOrDefault(item =>
                        string.Equals((string)item["Name"], equipment.EqpId, StringComparison.OrdinalIgnoreCase));

                    if (config == null)
                    {
                        config = CreateDefaultConnectionConfig(equipment);
                        foreach (var file in files)
                            file.Configs.Add((JObject)config.DeepClone());
                        WriteConnectionConfigFiles(files.Select(file => (file.Path, file.Configs.ToString(Formatting.Indented))).ToList());
                    }
                    else
                    {
                        var filesToUpdate = files.Where(file => !file.Configs.OfType<JObject>().Any(item =>
                            string.Equals((string)item["Name"], equipment.EqpId, StringComparison.OrdinalIgnoreCase))).ToList();
                        foreach (var file in filesToUpdate)
                            file.Configs.Add((JObject)config.DeepClone());
                        if (filesToUpdate.Count > 0)
                            WriteConnectionConfigFiles(filesToUpdate.Select(file => (file.Path, file.Configs.ToString(Formatting.Indented))).ToList());
                    }

                    bool inserted;
                    try
                    {
                        inserted = DataFactory.Current.Equipment.Insert(equipment);
                    }
                    catch (Exception insertError)
                    {
                        try
                        {
                            RestoreConnectionConfigFiles(originalFiles);
                        }
                        catch (Exception rollbackError)
                        {
                            throw new AggregateException("Equipment creation failed and configuration rollback could not restore all files.", insertError, rollbackError);
                        }
                        throw;
                    }

                    if (!inserted)
                    {
                        RestoreConnectionConfigFiles(originalFiles);
                        return Ok(new { Return = "NG", MESSAGE = "添加设备失败" });
                    }

                    return Ok(new
                    {
                        Return = "OK",
                        MESSAGE = "SUCCESS",
                        data = ToJsonElement(config),
                        files = files.Select(file => Path.GetFullPath(file.Path)).ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] Equipment equipment)
        {
            Equipment previous = null;
            var enableChanged = false;
            var configEnabledUpdated = false;
            try
            {
                previous = DataFactory.Current.Equipment.GetById(equipment.Id);
                if (previous == null)
                    return Ok(new { Return = "NG", MESSAGE = "设备不存在" });

                if (equipment.Enable == 0
                    && previous.EqpStatus == (int)EQPState.Connected
                    && previous.ControlState == (int)ControlState.REMOTE)
                {
                    var activeRun = DataFactory.Current.RunHistory.GetList(run =>
                            run.eqpId == previous.EqpId
                            && run.state == (int)LotRunState.Running
                            && run.endTime == null)
                        .FirstOrDefault();
                    var lotStarting = DataFactory.Current.Lot.GetList(lot =>
                            lot.EqpId == previous.EqpId
                            && lot.LotState == LotState.LotStart)
                        .FirstOrDefault();
                    var activeLotId = activeRun?.lotId ?? lotStarting?.LotId;
                    if (!string.IsNullOrWhiteSpace(activeLotId))
                    {
                        return Ok(new
                        {
                            Return = "NG",
                            MESSAGE = $"设备 {previous.EqpId} 当前在线且处于 Remote，Lot {activeLotId} 正在跑片，完成或停止跑片后才能禁用"
                        });
                    }
                }

                enableChanged = previous.Enable != equipment.Enable;
                if (enableChanged)
                {
                    SetConnectionConfigEnabled(equipment.EqpId, equipment.Enable == 1);
                    configEnabledUpdated = true;
                }

                var res = DataFactory.Current.Equipment.Update(equipment);
                if (res)
                {
                    if (enableChanged)
                    {
                        if (equipment.Enable == 1)
                            SendEquipmentLoopProcessStatesToEap(equipment.EqpId);

                        var controlResult = await SendEquipmentConnectionControlAsync(equipment.EqpId, equipment.Enable);
                        if (!controlResult.Success)
                        {
                            DataFactory.Current.Equipment.Update(previous);
                            SetConnectionConfigEnabled(equipment.EqpId, previous.Enable == 1);
                            return Ok(new { Return = "NG", MESSAGE = controlResult.Message });
                        }
                    }
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    if (configEnabledUpdated)
                        SetConnectionConfigEnabled(equipment.EqpId, previous.Enable == 1);
                    return Ok(new { Return = "NG", MESSAGE = "更新设备失败" });
                }
            }
            catch (Exception ex)
            {
                if (previous != null && enableChanged)
                {
                    DataFactory.Current.Equipment.Update(previous);
                    if (configEnabledUpdated)
                    {
                        try
                        {
                            SetConnectionConfigEnabled(previous.EqpId, previous.Enable == 1);
                        }
                        catch (Exception rollbackError)
                        {
                            return Ok(new { Return = "NG", MESSAGE = $"{ex.Message}; enable rollback failed: {rollbackError.Message}" });
                        }
                    }
                }
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        private static async Task<(bool Success, string Message)> SendEquipmentConnectionControlAsync(string equipmentId, int enable)
        {
            var request = new EquipmentConnectionControlRequest
            {
                Header = new MQHeader { MESSAGENAME = "EQUIPMENTCONNECTIONCONTROL" },
                Body = new EAPManage.Models.MQ.EquipmentConnectionControl
                {
                    EQUIPMENTID = equipmentId,
                    ENABLE = enable
                }
            };
            var response = await ActiveMQClient.Current.SendRequestAsync<EAPManage.Models.MQ.EquipmentConnectionControl>(
                XmlSerialization.XmlSerialize(request), TimeSpan.FromSeconds(15));
            if (response == null)
                return (false, "EAPServer未响应设备启停请求，请确认EAPServer和ActiveMQ正在运行");
            return (response.SUCCESS, response.MESSAGE);
        }

        private void SendEquipmentLoopProcessStatesToEap(string equipmentId)
        {
            try
            {
                var ports = DataFactory.Current.PortInfo.GetList(port => port.eqpId == equipmentId);
                foreach (var port in ports)
                    SendLoopProcessStateToMQ(equipmentId, port.portId, port.loopProcessState);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to refresh loop-process cache after enabling {equipmentId}: {ex.Message}");
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

        [HttpGet]
        [Route("GetTransferJobConfig")]
        public IActionResult GetTransferJobConfig(string eqpId)
        {
            try
            {
                var ports = string.IsNullOrWhiteSpace(eqpId)
                    ? DataFactory.Current.PortInfo.GetAll()
                    : DataFactory.Current.PortInfo.GetList(p => p.eqpId == eqpId);
                return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = ports.Select(ToTransferConfig).ToList() });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        [HttpPost]
        [Route("SetTransferJobConfig")]
        public IActionResult SetTransferJobConfig([FromBody] TransferJobConfigRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.eqpId))
                    return Ok(new { Return = "NG", MESSAGE = "eqpId is required" });
                if (request.ports == null || request.ports.Count == 0)
                    return Ok(new { Return = "NG", MESSAGE = "ports is required" });

                var configured = request.ports
                    .Select(p => new TransferPortConfigRequest
                    {
                        portId = p.portId,
                        jobMode = p.jobMode,
                        role = p.role,
                        partnerPortId = p.partnerPortId
                    }).ToList();
                var portIds = configured.Select(p => p.portId).ToHashSet();
                if (configured.Any(p => p.portId <= 0) || portIds.Count != configured.Count)
                    return Ok(new { Return = "NG", MESSAGE = "invalid portId" });
                if (configured.Any(p => p.jobMode < 0 || p.jobMode > 1 || p.role < 0 || p.role > 2))
                    return Ok(new { Return = "NG", MESSAGE = "invalid Transfer configuration values" });

                if (configured.Any(p => p.jobMode == 0 && (p.role != 0 || p.partnerPortId != 0)))
                    return Ok(new { Return = "NG", MESSAGE = "Original In/Out LP must not have Transfer role or partner" });
                var transferPorts = configured.Where(p => p.jobMode == 1).ToList();
                if (transferPorts.Any(p => p.role == 0 || p.partnerPortId <= 0 || p.partnerPortId == p.portId))
                    return Ok(new { Return = "NG", MESSAGE = "Transfer LP requires a role and a different partner LP" });
                foreach (var port in transferPorts)
                {
                    var partner = transferPorts.FirstOrDefault(p => p.portId == port.partnerPortId);
                    if (partner == null || partner.partnerPortId != port.portId || partner.role == port.role)
                        return Ok(new { Return = "NG", MESSAGE = "Transfer LPs must form a source/destination pair" });
                }

                var existing = DataFactory.Current.PortInfo.GetList(p => p.eqpId == request.eqpId);
                if (existing.Count != configured.Count || existing.Any(p => !portIds.Contains(p.portId)))
                    return Ok(new { Return = "NG", MESSAGE = "Configuration must include every LP on the equipment" });

                var existingById = existing.ToDictionary(p => p.portId);
                var affectedPortIds = configured
                    .Where(p => existingById[p.portId].jobMode != p.jobMode
                        || existingById[p.portId].transferRole != p.role
                        || existingById[p.portId].transferPartnerPortId != p.partnerPortId)
                    .Select(p => p.portId)
                    .ToHashSet();
                foreach (var port in existing.Where(p => affectedPortIds.Contains(p.transferPartnerPortId)))
                    affectedPortIds.Add(port.portId);
                foreach (var port in configured.Where(p => affectedPortIds.Contains(p.partnerPortId)))
                    affectedPortIds.Add(port.portId);

                if (affectedPortIds.Count > 0)
                {
                    var affectedPorts = existing.Where(p => affectedPortIds.Contains(p.portId)).ToList();
                    var activePort = affectedPorts.FirstOrDefault(p => p.loopProcessState != 1);
                    if (activePort != null)
                        return Ok(new { Return = "NG", MESSAGE = $"Stop LP{activePort.portId} before changing its job configuration" });

                    var activeRuns = DataFactory.Current.RunHistory.GetList(r => r.eqpId == request.eqpId && r.endTime == null);
                    var activeRun = activeRuns.FirstOrDefault(r => affectedPortIds.Contains(r.portId));
                    if (activeRun != null)
                        return Ok(new { Return = "NG", MESSAGE = $"LP{activeRun.portId} has an active lot; finish or cancel it before changing the configuration" });
                }

                DataFactory.Current.PortInfo.ExecuteTransaction(db =>
                {
                    foreach (var port in configured)
                    {
                        var affected = db.Updateable<PortInfo>()
                            .SetColumns(p => new PortInfo
                            {
                                jobMode = port.jobMode,
                                transferRole = port.role,
                                transferPartnerPortId = port.partnerPortId
                            })
                            .Where(p => p.eqpId == request.eqpId && p.portId == port.portId)
                            .ExecuteCommand();
                        if (affected != 1)
                        {
                            throw new InvalidOperationException($"Port {port.portId} update failed");
                        }
                    }
                });

                var current = DataFactory.Current.PortInfo.GetList(p => p.eqpId == request.eqpId);
                TransferJobConfigPublisher.Publish(request.eqpId, current);
                return Ok(new { Return = "OK", MESSAGE = "Transfer job configuration saved", data = current.Select(ToTransferConfig).ToList() });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        private static object ToTransferConfig(PortInfo p) => new
        {
            eqpId = p.eqpId,
            portId = p.portId,
            jobMode = p.jobMode,
            transferRole = p.transferRole,
            transferPartnerPortId = p.transferPartnerPortId
        };


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
