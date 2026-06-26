using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("Batch")]
    public class Batch
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "TID", ColumnDataType = "TEXT")]
        public string TID { get; set; }

        [SugarColumn(ColumnName = "EQP_ID", ColumnDataType = "TEXT")]
        public string EqpId { get; set; }

        [SugarColumn(ColumnName = "CARRIER_ID", ColumnDataType = "TEXT")]
        public string CarrierId { get; set; }

        [SugarColumn(ColumnName = "PORT_ID", ColumnDataType = "TEXT")]
        public string PortId { get; set; }

        [SugarColumn(ColumnName = "OPERATOR", ColumnDataType = "TEXT")]
        public string OPERATOR { get; set; }

        [SugarColumn(ColumnName = "BATCH_ID", ColumnDataType = "TEXT")]
        public string BatchId { get; set; }

        [SugarColumn(ColumnName = "BATCH_SIZE", ColumnDataType = "INTEGER")]
        public int BatchSize { get; set; }

        [SugarColumn(IsIgnore = true)]
        public List<string> lots { get; set; }
    }
}
