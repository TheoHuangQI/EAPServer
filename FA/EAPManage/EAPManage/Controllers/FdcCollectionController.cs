using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FdcCollectionController : ControllerBase
    {
        [HttpGet("svid-definitions")]
        public IActionResult GetSvidDefinitions(string eqpId = "AHEG07", string keyword = "", int pageNo = 1, int pageSize = 50)
        {
            try
            {
                var definitions = LoadSvidDefinitions(eqpId);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var normalizedKeyword = keyword.Trim().ToLowerInvariant();
                    definitions = definitions
                        .Where(item =>
                            item.Svid.ToString().Contains(normalizedKeyword) ||
                            (item.Name ?? string.Empty).ToLowerInvariant().Contains(normalizedKeyword) ||
                            (item.EcsName ?? string.Empty).ToLowerInvariant().Contains(normalizedKeyword))
                        .ToList();
                }

                var total = definitions.Count;
                pageNo = Math.Max(pageNo, 1);
                pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 500);

                var list = definitions
                    .Skip((pageNo - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    Return = "OK",
                    MESSAGE = "SUCCESS",
                    data = new
                    {
                        total,
                        list
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        [HttpGet("latest")]
        public IActionResult GetLatestValues(string eqpId = "", int limit = 500)
        {
            try
            {
                var query = DataFactory.Current.VIDValue.Queryable();
                if (!string.IsNullOrWhiteSpace(eqpId))
                {
                    query = query.Where(item => item.eqpId == eqpId);
                }

                limit = limit <= 0 ? 500 : Math.Min(limit, 2000);
                var latestRows = query
                    .OrderBy(item => item.time, SqlSugar.OrderByType.Desc)
                    .ToList()
                    .GroupBy(item => new { item.eqpId, item.VId })
                    .Select(group => group.First())
                    .Take(limit)
                    .Select(item => new
                    {
                        eqpId = item.eqpId,
                        vid = item.VId,
                        name = item.name,
                        type = item.type,
                        value = item.value,
                        time = item.time,
                        abnormalFlag = item.AbnormalFlag
                    })
                    .ToList();

                return Ok(new
                {
                    Return = "OK",
                    MESSAGE = "SUCCESS",
                    data = new
                    {
                        total = latestRows.Count,
                        list = latestRows
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = ex.Message });
            }
        }

        [HttpPost("collect")]
        public IActionResult Collect([FromBody] FdcCollectRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EqpId))
            {
                return Ok(new { Return = "NG", MESSAGE = "设备号不能为空" });
            }

            var result = EAPClient.CollectFdcSvidValues(
                request.EqpId,
                request.Svids ?? new List<int>(),
                request.SaveToHistory,
                request.MaxCount,
                request.ChunkSize);

            if (result == null)
            {
                return Ok(new { Return = "NG", MESSAGE = "FDC S1F3 采集请求超时或 EAPServer 未响应" });
            }

            var saved = request.SaveToHistory ? SaveCollectedValues(result) : 0;

            return Ok(new
            {
                Return = result.Return == "OK" ? "OK" : "NG",
                MESSAGE = result.Message,
                data = new
                {
                    eqpId = result.EQUIPMENTID,
                    collectedAt = result.CollectedAt,
                    total = result.Values?.Count ?? 0,
                    success = result.Values?.Count(item => item.Status == "OK") ?? 0,
                    failed = result.Values?.Count(item => item.Status != "OK") ?? 0,
                    saved,
                    list = result.Values ?? new List<FdcSvidValue>()
                }
            });
        }

        [HttpPost("collect/rules")]
        public IActionResult CollectByRules([FromBody] FdcCollectByRulesRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EqpId))
            {
                return Ok(new { Return = "NG", MESSAGE = "设备号不能为空" });
            }

            var rules = DataFactory.Current.DataRule.GetList(rule => rule.eqpId == request.EqpId && rule.vidType == 1);
            var svids = rules
                .Select(rule => int.TryParse(rule.vid, out var svid) ? svid : 0)
                .Where(svid => svid > 0)
                .Distinct()
                .ToList();

            if (svids.Count == 0)
            {
                return Ok(new { Return = "NG", MESSAGE = "当前设备没有可用于 S1F3 的 SVID 采集规则" });
            }

            return Collect(new FdcCollectRequest
            {
                EqpId = request.EqpId,
                Svids = svids,
                SaveToHistory = request.SaveToHistory,
                MaxCount = request.MaxCount,
                ChunkSize = request.ChunkSize
            });
        }

        private static int SaveCollectedValues(FdcSvidCollection result)
        {
            if (result?.Values == null || result.Values.Count == 0)
            {
                return 0;
            }

            var latestByVid = DataFactory.Current.VIDValue
                .Queryable()
                .Where(item => item.eqpId == result.EQUIPMENTID)
                .OrderBy(item => item.time, SqlSugar.OrderByType.Desc)
                .ToList()
                .GroupBy(item => item.VId)
                .ToDictionary(group => group.Key, group => group.First());

            var rows = new List<VIDValue>();
            foreach (var item in result.Values.Where(item => item.Status == "OK"))
            {
                rows.Add(new VIDValue
                {
                    eqpId = result.EQUIPMENTID,
                    VId = item.Svid,
                    type = string.IsNullOrWhiteSpace(item.ActualType) ? item.DefinedType : item.ActualType,
                    value = item.Value,
                    name = item.Name,
                    time = result.CollectedAt,
                    AbnormalFlag = ResolveAbnormalFlag(item, latestByVid)
                });
            }

            return rows.Count == 0 ? 0 : DataFactory.Current.VIDValue.Insert(rows);
        }

        private static int ResolveAbnormalFlag(FdcSvidValue current, Dictionary<int, VIDValue> latestByVid)
        {
            latestByVid.TryGetValue(current.Svid, out var latest);

            if (current.Value == "0")
            {
                return (int)VIDAbnormalType.ZeroValue;
            }

            if (latest == null)
            {
                return (int)VIDAbnormalType.FirstValue;
            }

            if (!string.IsNullOrWhiteSpace(current.Value) &&
                string.Equals(latest.value, current.Value, StringComparison.OrdinalIgnoreCase))
            {
                return (int)VIDAbnormalType.SameAsPrevious;
            }

            return (int)VIDAbnormalType.None;
        }

        private static List<FdcSvidValue> LoadSvidDefinitions(string eqpId)
        {
            var path = ResolveSvidDefinitionPath(eqpId);
            if (!System.IO.File.Exists(path))
            {
                throw new FileNotFoundException($"SVID definition file not found: {path}");
            }

            return System.IO.File.ReadAllLines(path, Encoding.UTF8)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("id,", StringComparison.OrdinalIgnoreCase))
                .Select(ReadCsvFields)
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out _))
                .Select(parts => new FdcSvidValue
                {
                    Svid = int.Parse(parts[0]),
                    Name = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                    DefinedType = parts.Length > 2 ? parts[2].Trim() : string.Empty,
                    Unit = parts.Length > 3 ? parts[3].Trim() : string.Empty,
                    EcsName = parts.Length > 5 ? parts[5].Trim() : string.Empty,
                    Description = parts.Length > 6 ? parts[6].Trim() : string.Empty,
                    Status = "DEFINED"
                })
                .OrderBy(item => item.Svid)
                .ToList();
        }

        private static string ResolveSvidDefinitionPath(string eqpId)
        {
            var configName = string.IsNullOrWhiteSpace(eqpId) ? "AHEG07" : eqpId;
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "Configuration", "conf", configName, "ecs_sv_def.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "conf", configName, "ecs_sv_def.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "EAPServer", "Configuration", "conf", configName, "ecs_sv_def.csv")
            };

            foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var directory = new DirectoryInfo(root);
                while (directory != null)
                {
                    candidates.Add(Path.Combine(directory.FullName, "EAPServer", "Configuration", "conf", configName, "ecs_sv_def.csv"));
                    candidates.Add(Path.Combine(directory.FullName, "Configuration", "conf", configName, "ecs_sv_def.csv"));
                    directory = directory.Parent;
                }
            }

            return candidates.FirstOrDefault(System.IO.File.Exists) ?? candidates[0];
        }

        private static string[] ReadCsvFields(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }

    public class FdcCollectRequest
    {
        public string EqpId { get; set; }

        public List<int> Svids { get; set; } = new List<int>();

        public bool SaveToHistory { get; set; } = true;

        public int MaxCount { get; set; }

        public int ChunkSize { get; set; } = 120;
    }

    public class FdcCollectByRulesRequest
    {
        public string EqpId { get; set; }

        public bool SaveToHistory { get; set; } = true;

        public int MaxCount { get; set; }

        public int ChunkSize { get; set; } = 120;
    }
}
