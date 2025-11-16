using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("DATA_HISTORY")]
    public class DataHistory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "VID", ColumnDataType = "TEXT")]
        public string vid { get; set; }

        [SugarColumn(ColumnName = "UUID", ColumnDataType = "TEXT")]
        public string uuid { get; set; }

        [SugarColumn(ColumnName = "VALUE", ColumnDataType = "TEXT")]
        public string value { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string eqpId { get; set; }

        [SugarColumn(ColumnName = "LOTID", ColumnDataType = "TEXT")]
        public string lotId { get; set; }

        [SugarColumn(ColumnName = "CREATE_TIME", ColumnDataType = "TEXT")]
        public string creatTime { get; set; }

        [SugarColumn(ColumnName = "FLAG", ColumnDataType = "INTEGER")]
        public string flag { get; set; }
    }
}
