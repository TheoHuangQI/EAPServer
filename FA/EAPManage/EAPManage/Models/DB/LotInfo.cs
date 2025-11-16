using EAPManage.Models.Enum;
using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("Lot")]
    public class LotInfo
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "LOT_ID", ColumnDataType = "TEXT")]
        public string LotId { get; set; }

        [SugarColumn(ColumnName = "QTY", ColumnDataType = "INTEGER")]
        public int QTY { get; set; }

        [SugarColumn(ColumnName = "PPID", ColumnDataType = "TEXT")]
        public string PPID { get; set; }

        [SugarColumn(ColumnName = "BATCH_ID", ColumnDataType = "TEXT")]
        public string BatchId { get; set; }

        [SugarColumn(ColumnName = "LOT_STATE", ColumnDataType = "INTEGER")]
        public LotState LotState { get; set; }

        [SugarColumn(IsIgnore = true)]
        public List<string> Slots { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string SlotsStr { get; set; }
    }
}
