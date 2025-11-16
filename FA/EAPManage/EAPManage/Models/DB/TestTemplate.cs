using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("TEST_TEMPLATE")]
    public class TestTemplate
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "TEMP_NAME", ColumnDataType = "TEXT")]
        public string tempName { get; set; }
    }
}
