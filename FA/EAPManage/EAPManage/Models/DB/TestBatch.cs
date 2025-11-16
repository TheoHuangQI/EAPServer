using EAPManage.Models.Enum;
using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("TEST_BATCH")]
    public class TestBatch
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "TEST_BATCH_NAME", ColumnDataType = "TEXT")]
        public string TestBatchName { get; set; }

        [SugarColumn(ColumnName = "STARTE_EVENT", ColumnDataType = "TEXT")]
        public string StartEvent { get; set; }

        [SugarColumn(ColumnName = "END_EVENT", ColumnDataType = "TEXT")]
        public string EndEvent { get; set; }
        [SugarColumn(ColumnName = "TEST_TYPE", ColumnDataType = "INTEGER")]
        public TestType TestType { get; set; }
        
    }
}
