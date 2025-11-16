using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("DATA_RULE")]
    public class DataRule
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "VID", ColumnDataType = "TEXT")]
        public string vid { get; set; }

        [SugarColumn(ColumnName = "VID_TYPE", ColumnDataType = "INTEGER")]
        public int vidType { get; set; }

        [SugarColumn(ColumnName = "INTERVAL", ColumnDataType = "INTEGER")]
        public int interval { get; set; }

        [SugarColumn(ColumnName = "COUNT", ColumnDataType = "INTEGER")]
        public int count { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string eqpId { get; set; }

        [SugarColumn(ColumnName = "TRIGGER_TYPE", ColumnDataType = "INTEGER")]
        public int triggerType { get; set; }

        [SugarColumn(ColumnName = "START_EVENT", ColumnDataType = "TEXT")]
        public string startEvent { get; set; }

        [SugarColumn(ColumnName = "END_EVENT", ColumnDataType = "TEXT")]
        public string endEvent { get; set; }

        [SugarColumn(ColumnName = "COMPARE_TYPE", ColumnDataType = "INTEGER")]
        public int compareType { get; set; }

        [SugarColumn(ColumnName = "COMPARE_VALUE", ColumnDataType = "TEXT")]
        public string compareValue { get; set; }

        [SugarColumn(ColumnName = "COMPARE_MAX", ColumnDataType = "TEXT")]
        public string compareMax { get; set; }

        [SugarColumn(ColumnName = "COMPARE_MIN", ColumnDataType = "TEXT")]
        public string compareMin { get; set; }

        [SugarColumn(ColumnName = "LIMIT_TIMES", ColumnDataType = "INTEGER")]
        public int limitTimes { get; set; }

        [SugarColumn(ColumnName = "ACTION", ColumnDataType = "TEXT")]
        public string action { get; set; }

        [SugarColumn(IsIgnore = true)]
        public List<int> actions { get; set; }
    }
}
