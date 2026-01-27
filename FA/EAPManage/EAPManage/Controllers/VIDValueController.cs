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
        public IActionResult Get(string eqpId, string startTime, string endTime)
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
            var result = values
                .GroupBy(v => new { v.VId, v.name })
                .Select(g => new
                {
                    vid = g.Key.VId,
                    name = g.Key.name,
                    points = g.Select(x => new
                    {
                        time = x.time,
                        value = ParseDecimal(x.value)
                    }).ToList()
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
        public IActionResult GetWithAbnormal(string eqpId, string startTime, string endTime, int AbnormalFlag = -1)
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

            var result = values
                .GroupBy(v => new { v.VId, v.name })
                .Select(g => new
                {
                    vid = g.Key.VId,
                    name = g.Key.name,
                    points = g.Select(x => new
                    {
                        time = x.time,
                        value = ParseDecimal(x.value)
                    }).ToList()
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
    }
}
