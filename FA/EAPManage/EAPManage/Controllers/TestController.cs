using EAPManage.DataBase;
using EAPManage.Models;
using EAPManage.Models.DB;
using EAPManage.Models.MQ;
using EAPManage.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace EAPManage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {

        #region 单测试项
        [HttpGet]
        [Route("TestItem")]
        public IActionResult TestItem(string messageName)
        {
            List<TestItem> TestItemList = new List<TestItem>();
            if (string.IsNullOrEmpty(messageName))
            {
                TestItemList = DataFactory.Current.TestItem.GetAll();
            }
            else
            {
                TestItemList = DataFactory.Current.TestItem.GetList(a=>a.MessageName.Contains(messageName));
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", TestItemList.Count);
            resdata.Add("list", TestItemList);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPost]
        [Route("TestItem")]
        public IActionResult TestItem([FromBody] TestItem item)
        {
            try
            {
                bool res = DataFactory.Current.TestItem.Insert(item);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS"});
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
            }
        }

        [HttpPut]
        [Route("PutTestItem")]
        public IActionResult PutTestItem([FromBody] TestItem item)
        {
            try
            {
                bool res = DataFactory.Current.TestItem.Update(item);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加Lot失败" });
            }
        }

        [HttpDelete]
        [Route("TestItem")]
        public IActionResult TestItem([FromBody] List<string> messageNames)
        {
            try
            {
                var res = false;
                DataFactory.Current.TestItem.ExecuteTransaction(db =>
                {
                    foreach(var messageName in messageNames)
                    {
                       res=DataFactory.Current.TestItem.Delete(a=>a.MessageName==messageName)>0?true:false;
                    }
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "删除测试项成功" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除测试项失败" });
                }
                
            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除测试项失败" });
            }
        }
        #endregion

        #region 复合测试项
        [HttpGet]
        [Route("TestBatch")]
        public IActionResult TestBatch()
        {
            List<TestBatch> TestBatchList = DataFactory.Current.TestBatch.GetAll();
            List<TestBatchItems> testBatchItemsList = new List<TestBatchItems>();
            foreach (var testBatch in TestBatchList)
            {
                List<TestBatchItem> testBatchItemList = DataFactory.Current.TestBatchItem.GetList(u => u.TestBatchId == testBatch.Id);
                TestBatchItems testBatchItems = new TestBatchItems
                {
                    TestBatchId = testBatch.Id,
                    TestBatchName = testBatch.TestBatchName,
                    TestItems = new List<TestItem>()
                };
                foreach(var testBatchItem in testBatchItemList)
                {
                    testBatchItems.TestItems.AddRange(DataFactory.Current.TestItem.GetList(u => u.Id == testBatchItem.TestItemId));
                }
                testBatchItemsList.Add(testBatchItems);
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", testBatchItemsList.Count);
            resdata.Add("list", testBatchItemsList);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }


        [HttpGet]
        [Route("GetAllBatch")]
        public IActionResult GetAllBatch()
        {
            List<TestBatch> TestBatchList = DataFactory.Current.TestBatch.GetAll();
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", TestBatchList.Count);
            resdata.Add("list", TestBatchList);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPut]
        [Route("saveTestBatch")]
        public IActionResult saveTestBatch([FromBody] TestBatchItems testBatchItems)
        {
            try
            {
                bool res = true;
                DataFactory.Current.TestBatchItem.ExecuteTransaction(db =>
                {
                    int con = DataFactory.Current.TestBatchItem.Delete(u => u.TestBatchId == testBatchItems.TestBatchId);
                    int i = 1;
                    foreach (var testItem in testBatchItems.TestItems)
                    {
                        res = DataFactory.Current.TestBatchItem.Insert(new TestBatchItem()
                        {
                            TestBatchId = testBatchItems.TestBatchId,
                            TestBatchName = testBatchItems.TestBatchName,
                            TestItemId = testItem.Id,
                            TestItemName = testItem.MessageName,
                            SerialNumber=i
                        });
                        i++;
                    }
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "保存复合测试项失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "保存复合测试项失败" });
            }
        }

        [HttpPost]
        [Route("TestBatch")]
        public IActionResult TestBatch([FromBody] TestBatchItems testBatchItems)
        {
            try
            {
                var testBatchs = DataFactory.Current.TestBatch.GetList(u => u.TestBatchName == testBatchItems.TestBatchName);
                if(testBatchs != null && testBatchs.Count > 0)
                {
                    return Ok(new { Return = "NG", MESSAGE = "所添加的测试项已存在" });
                }
                bool res = DataFactory.Current.TestBatch.Insert(new TestBatch() { TestBatchName = testBatchItems.TestBatchName });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加复合测试项失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加复合测试项失败" });
            }
        }
        #endregion

        #region 测试模版
        [HttpGet]
        [Route("TestTemplate")]
        public IActionResult TestTemplate()
        {
            List<TestTemplate> testTemplateList = DataFactory.Current.TestTemplate.GetAll();
            List<TestTemplateItems> testTemplateItems = new List<TestTemplateItems>();
            foreach (var testTemplate in testTemplateList)
            {
                List<TestTemplateItem> testTemplateItemList = DataFactory.Current.TestTemplateItem.GetList(u => u.TempId == testTemplate.Id);

                TestTemplateItems templateItems = new TestTemplateItems
                {
                    TestTemplateId = testTemplate.Id,
                    TestTemplateName = testTemplate.tempName,
                    TestItems = new List<TestItem>(),
                    TestBatchs = new List<TestBatch>()
                };
                foreach (var testTemplateItem in testTemplateItemList)
                {
                    if(testTemplateItem.TestItemId!=0 && !string.IsNullOrEmpty(testTemplateItem.TestItemName))
                    {
                        templateItems.TestItems.AddRange(DataFactory.Current.TestItem.GetList(u => u.Id == testTemplateItem.TestItemId));
                    }

                    if (testTemplateItem.TestBatchId != 0 && !string.IsNullOrEmpty(testTemplateItem.TestBatchName))
                    {
                        templateItems.TestBatchs.AddRange(DataFactory.Current.TestBatch.GetList(u => u.Id == testTemplateItem.TestBatchId));
                    }
                }
                testTemplateItems.Add(templateItems);
            }
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", testTemplateItems.Count);
            resdata.Add("list", testTemplateItems);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpGet]
        [Route("GetAllTemplate")]
        public IActionResult GetAllTemplate()
        {
            List<TestTemplate> testTemplateList = DataFactory.Current.TestTemplate.GetAll();
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", testTemplateList.Count);
            resdata.Add("list", testTemplateList);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }

        [HttpPut]
        [Route("saveTestTemplate")]
        public IActionResult saveTestTemplate([FromBody] TestTemplateItems testTemplateItems)
        {
            try
            {
                bool res = true;
                DataFactory.Current.TestTemplateItem.ExecuteTransaction(db =>
                {
                    int con = DataFactory.Current.TestTemplateItem.Delete(u => u.TempId == testTemplateItems.TestTemplateId);
                    int i = 1;
                    foreach (var testItem in testTemplateItems.TestItems)
                    {
                        res = DataFactory.Current.TestTemplateItem.Insert(new TestTemplateItem()
                        {
                            TempId = testTemplateItems.TestTemplateId,
                            TempName = testTemplateItems.TestTemplateName,
                            TestItemId = testItem.Id,
                            TestItemName = testItem.MessageName,
                            SerialNumber = i
                        });
                        i++;
                    }

                    foreach (var testBatch in testTemplateItems.TestBatchs)
                    {
                        res = DataFactory.Current.TestTemplateItem.Insert(new TestTemplateItem()
                        {
                            TempId = testTemplateItems.TestTemplateId,
                            TempName = testTemplateItems.TestTemplateName,
                            TestBatchId = testBatch.Id,
                            TestBatchName = testBatch.TestBatchName,
                            SerialNumber = i
                        });
                        i++;
                    }
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "保存测试模版失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "保存测试模版失败" });
            }
        }

        [HttpPost]
        [Route("TestTemplate")]
        public IActionResult TestTemplate([FromBody] TestTemplateItems testTemplateItems)
        {
            try
            {
                var testTemplates = DataFactory.Current.TestTemplate.GetList(u => u.tempName == testTemplateItems.TestTemplateName);
                if (testTemplates != null && testTemplates.Count > 0)
                {
                    return Ok(new { Return = "NG", MESSAGE = "所添加测试模版名称已存在" });
                }
                bool res = DataFactory.Current.TestTemplate.Insert(new TestTemplate() { tempName = testTemplateItems.TestTemplateName });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加测试模版失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加测试模版失败" });
            }
        }
        #endregion

        #region 测试用例
        [HttpGet]
        [Route("TestCase")]
        public IActionResult TestCase()
        {
            List<TestCase> TestCaseList = DataFactory.Current.TestCase.GetAll();
            Dictionary<string, object> resdata = new Dictionary<string, object>();
            resdata.Add("total", TestCaseList.Count);
            resdata.Add("list", TestCaseList);
            return Ok(new { Return = "OK", MESSAGE = "SUCCESS", data = resdata });
        }


        [HttpPost]
        [Route("TestCase")]
        public IActionResult TestCase([FromBody] TestCase testCase)
        {
            try
            {
                bool res = DataFactory.Current.TestCase.Insert(testCase);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "添加测试用例失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加测试用例失败" });
            }
        }

        [HttpPut]
        [Route("TestCase")]
        public IActionResult SaveTestCase([FromBody] TestCase testCase)
        {
            try
            {
                bool res = DataFactory.Current.TestCase.Update(testCase);
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "保存测试用例失败" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "保存测试用例失败" });
            }
        }

        [HttpDelete]
        [Route("TestCase")]
        public IActionResult TestCase([FromBody] List<int> ids)
        {
            try
            {
                var res = false;
                DataFactory.Current.TestItem.ExecuteTransaction(db =>
                {
                    foreach (var id in ids)
                    {
                        res = DataFactory.Current.TestCase.Delete(a => a.Id == id) > 0 ? true : false;
                    }
                });
                if (res)
                {
                    return Ok(new { Return = "OK", MESSAGE = "删除测试用例成功" });
                }
                else
                {
                    return Ok(new { Return = "NG", MESSAGE = "删除测试用例失败" });
                }

            }
            catch
            {
                return Ok(new { Return = "NG", MESSAGE = "删除测试用例失败" });
            }
        }


        [HttpPost]
        [Route("StartCase")]
        public IActionResult StartCase([FromBody] TestCase testCase)
        {
            try
            {
                //开启新现场执行测试任务
                Task.Run(() =>EAPClient.StartTest(testCase)).Start();
                return Ok(new { Return = "OK", MESSAGE = "SUCCESS" });
            }
            catch (Exception ex)
            {
                return Ok(new { Return = "NG", MESSAGE = "添加测试用例失败" });
            }
        }
        #endregion

    }
}