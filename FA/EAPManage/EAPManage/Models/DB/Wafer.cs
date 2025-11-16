using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("Wafer")]
    public class Wafer
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "WAFER_ID", ColumnDataType = "TEXT")]
        public string WaferId { get; set; }

        [SugarColumn(ColumnName = "SLOT", ColumnDataType = "INTEGER")]
        public int Slot { get; set; }

        [SugarColumn(ColumnName = "LOT_ID", ColumnDataType = "TEXT")]
        public string lotId { get; set; }
    }
}
