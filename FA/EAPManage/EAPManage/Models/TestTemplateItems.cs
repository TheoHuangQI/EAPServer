using EAPManage.Models.DB;

namespace EAPManage.Models
{
    public class TestTemplateItems
    {
        public int TestTemplateId { get; set; }
        public string TestTemplateName { get; set; }
        public List<TestItem> TestItems { get; set; }
        public List<TestBatch> TestBatchs { get; set; }
    }
}
