using EAPManage.Models.DB;
using SqlSugar;

namespace EAPManage.Models
{
    public class TestBatchItems
    {
        public int TestBatchId { get; set; }

        public List<TestItem> TestItems { get; set; }

        public string TestBatchName { get; set; }
    }
}
