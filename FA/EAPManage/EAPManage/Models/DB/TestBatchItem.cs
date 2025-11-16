using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("TEST_BATCH_ITEM")]
    public class TestBatchItem
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "TEST_BATCH_ID", ColumnDataType = "INTEGER")]
        public int TestBatchId { get; set; }

        [SugarColumn(ColumnName = "TEST_ITEM_ID", ColumnDataType = "INTEGER")]
        public int TestItemId { get; set; }

        [SugarColumn(ColumnName = "TEST_BATCH_NAME", ColumnDataType = "TEXT")]
        public string TestBatchName { get; set; }

        [SugarColumn(ColumnName = "TEST_ITEM_NAME", ColumnDataType = "TEXT")]
        public string TestItemName { get; set; }

        [SugarColumn(ColumnName = "SERIAL_NUMBER", ColumnDataType = "INTEGER")]
        public int SerialNumber { get; set; }
    }
}
