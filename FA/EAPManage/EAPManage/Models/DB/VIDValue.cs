using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("VIDValue")]
    public class VIDValue
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "VID", ColumnDataType = "INTEGER")]
        public int VId { get; set; }

        [SugarColumn(ColumnName = "TYPE", ColumnDataType = "TEXT")]
        public string type { get; set; }

        [SugarColumn(ColumnName = "VALUE", ColumnDataType = "TEXT")]
        public string value { get; set; }

        [SugarColumn(ColumnName = "NAME", ColumnDataType = "TEXT")]
        public string name { get; set; }

        [SugarColumn(ColumnName = "TIME", ColumnDataType = "TEXT")]
        public string time { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string eqpId { get; set; }

        [SugarColumn(ColumnName = "ABNORMAL_FLAG", ColumnDataType = "INTEGER")]
        public int AbnormalFlag { get; set; }
    }

    [Flags]
    public enum VIDAbnormalType
    {
        None = 0,

        SameAsPrevious = 1 << 0, // 与上一次值相同
        ZeroValue = 1 << 1, // 值为 0
        FirstValue = 1 << 2, // 首次采集
    }
}
