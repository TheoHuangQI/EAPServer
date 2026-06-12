using EAPManage.DataBase;
using EAPManage.Models.DB;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Globalization;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VIDValueController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get(string eqpId, string startTime, string endTime, int maxPointsPerSeries = 400)
        {
            if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                return Ok(new { Return = "NG", MESSAGE = "开始时间和结束时间不能为空" });
            }

            if (!TryParseDbTime(startTime, out DateTime start) ||
                !TryParseDbTime(endTime, out DateTime end))
            {
                return Ok(new { Return = "NG", MESSAGE = "时间格式不正确" });
            }

            // 1. SqlSugar Queryable（数据库层过滤）
            var query = DataFactory.Current.VIDValue
                .Queryable();

            if (!string.IsNullOrEmpty(eqpId))
            {
                query = query.Where(v => v.eqpId == eqpId);
            }

            // SQLite：字符串时间可直接比较
            query = query.Where(v => v.time.CompareTo(startTime) >= 0 &&
                                     v.time.CompareTo(endTime) <= 0);

            var values = query
                .OrderBy(v => v.time)
                .ToList();

            // 2. 分组并整理为趋势结构
            var pointLimit = NormalizeMaxPoints(maxPointsPerSeries);
            var result = values
                .GroupBy(v => new { v.VId, v.name })
                .Select(g => new
                {
                    vid = g.Key.VId,
                    name = g.Key.name,
                    points = DownsamplePoints(g, pointLimit)
                })
                .ToList();

            return Ok(new
            {
                Return = "OK",
                MESSAGE = "SUCCESS",
                data = result
            });
        }

        [HttpGet("withAbnormal")]
        public IActionResult GetWithAbnormal(string eqpId, string startTime, string endTime, int AbnormalFlag = -1, int maxPointsPerSeries = 400)
        {
            if (string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                return Ok(new { Return = "NG", MESSAGE = "开始时间和结束时间不能为空" });
            }

            if (!TryParseDbTime(startTime, out DateTime start) ||
                !TryParseDbTime(endTime, out DateTime end))
            {
                return Ok(new { Return = "NG", MESSAGE = "时间格式不正确" });
            }

            var query = DataFactory.Current.VIDValue
                .Queryable();

            if (!string.IsNullOrEmpty(eqpId))
            {
                query = query.Where(v => v.eqpId == eqpId);
            }

            query = query.Where(v =>
                v.time.CompareTo(startTime) >= 0 &&
                v.time.CompareTo(endTime) <= 0);

            if (AbnormalFlag >= 0)
            {
                query = query.Where(v => v.AbnormalFlag == AbnormalFlag);
            }

            var values = query
                .OrderBy(v => v.time)
                .ToList();

            var pointLimit = NormalizeMaxPoints(maxPointsPerSeries);
            var result = values
                .GroupBy(v => new { v.VId, v.name })
                .Select(g => new
                {
                    vid = g.Key.VId,
                    name = g.Key.name,
                    points = DownsamplePoints(g, pointLimit)
                })
                .ToList();

            return Ok(new
            {
                Return = "OK",
                MESSAGE = "SUCCESS",
                data = result
            });
        }

        /// <summary>
        /// 解析数据库 time 字段
        /// </summary>
        private static bool TryParseDbTime(string time, out DateTime result)
        {
            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd  HH:mm:ss",
                "yyyy-MM-dd  HH:mm:ss:fff"
            };

            return DateTime.TryParseExact(
                time,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result
            );
        }

        private static decimal ParseDecimal(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return 0;
        }

        private static int NormalizeMaxPoints(int maxPointsPerSeries)
        {
            if (maxPointsPerSeries < 50)
            {
                return 50;
            }

            if (maxPointsPerSeries > 2000)
            {
                return 2000;
            }

            return maxPointsPerSeries;
        }

        private static List<TrendPoint> DownsamplePoints(IEnumerable<VIDValue> values, int maxPoints)
        {
            var list = values.ToList();
            if (list.Count <= maxPoints)
            {
                return list.Select(ToTrendPoint).ToList();
            }

            var result = new List<TrendPoint>(maxPoints);
            var step = (list.Count - 1) / (double)(maxPoints - 1);
            var lastIndex = -1;

            for (var i = 0; i < maxPoints; i++)
            {
                var index = (int)Math.Round(i * step);
                if (index >= list.Count)
                {
                    index = list.Count - 1;
                }

                if (index == lastIndex)
                {
                    continue;
                }

                result.Add(ToTrendPoint(list[index]));
                lastIndex = index;
            }

            return result;
        }

        private static TrendPoint ToTrendPoint(VIDValue value)
        {
            return new TrendPoint
            {
                time = value.time,
                value = ParseDecimal(value.value),
                abnormalFlag = value.AbnormalFlag
            };
        }

        private class TrendPoint
        {
            public string time { get; set; }
            public decimal value { get; set; }
            public int abnormalFlag { get; set; }
        }
    }
}
