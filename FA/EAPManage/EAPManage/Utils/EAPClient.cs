using EAPManage.DataBase;
using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using EAPManage.Models.MQ;

namespace EAPManage.Utils
{
    public class EAPClient
    {
        #region Get_PPID_List 请求设备Recipe列表
        /// <summary>
        /// 请求设备Recipe列表
        /// </summary>
        public static List<string> Get_PPID_List(string eqpId)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "GETPPIDLIST";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;
                EqpPPIDList eqpPPIDList = new EqpPPIDList();
                eqpPPIDList.EQUIPMENTID = eqpId;
                var xmlBody = XmlSerialization.SerializeOnlyBody(eqpPPIDList);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData = ActiveMQClient.Current.SendRequestAsync<EqpPPIDList>(xmlMessage).Result;
                if (replyData == null)
                    return new List<string>();
                return replyData.PPLIST;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static FdcSvidCollection CollectFdcSvidValues(string eqpId, List<int> svids, bool saveToHistory, int maxCount = 0, int chunkSize = 120)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "FDCSVIDCOLLECT";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;

                var body = new FdcSvidCollection
                {
                    EQUIPMENTID = eqpId,
                    RequestedSvids = svids ?? new List<int>(),
                    SaveToHistory = saveToHistory,
                    MaxCount = maxCount,
                    ChunkSize = chunkSize
                };

                var xmlBody = XmlSerialization.SerializeOnlyBody(body);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                return ActiveMQClient.Current.SendRequestAsync<FdcSvidCollection>(xmlMessage).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public static void StartTest(TestCase testCase)
        {
            try
            {
                TestTemplate template = DataFactory.Current.TestTemplate.GetById(testCase.TempId);
                List<TestTemplateItem> testTemplateItemList = DataFactory.Current.TestTemplateItem.GetList(u => u.TempId == template.Id);
                testTemplateItemList = (List<TestTemplateItem>)testTemplateItemList.OrderBy(x => x.SerialNumber).ToList();
                if (testTemplateItemList == null || testTemplateItemList.Count == 0)
                {
                    return;
                }
                EAPTestCase eapTestCase = new EAPTestCase();
                eapTestCase.TestCaseName = testCase.TestCaseName;
                eapTestCase.EAPTestTemplate = new EAPTestTemplate();
                eapTestCase.EAPTestTemplate.TestTempName = testCase.TempName;
                eapTestCase.EAPTestTemplate.TestItems = new List<EAPTestItem>();
                eapTestCase.EAPTestTemplate.TestBatches = new List<EAPTestBatch>();
                foreach (var item in testTemplateItemList)
                {
                    if (item.TestBatchId != 0 && !string.IsNullOrEmpty(item.TestBatchName))
                    {
                        TestBatch testBatch = DataFactory.Current.TestBatch.GetById(item.TestBatchId);
                        EAPTestBatch eapTestBatch = new EAPTestBatch();
                        eapTestBatch.TestBatchName = item.TestBatchName;
                        eapTestBatch.SerialNumber = item.SerialNumber;
                        eapTestBatch.StartEvent = testBatch.StartEvent;
                        eapTestBatch.EndEvent = testBatch.EndEvent;
                        eapTestBatch.TestType = testBatch.TestType;
                        eapTestBatch.TestItems = new List<EAPTestItem>();

                        List<TestBatchItem> testBatchItemList = DataFactory.Current.TestBatchItem.GetList(u => u.TestBatchId == item.TestBatchId);

                        if (testBatchItemList != null && testBatchItemList.Count > 0)
                        {
                            foreach (var batchItem in testBatchItemList)
                            {
                                TestItem testItem = DataFactory.Current.TestItem.GetById(batchItem.TestItemId);
                                EAPTestItem eapTestItem = new EAPTestItem()
                                {
                                    SerialNumber = batchItem.SerialNumber,
                                    MessageName = testItem.MessageName,
                                    Description = testItem.Description,
                                    Stream = testItem.Stream,
                                    Function = testItem.Function,
                                    Direction = testItem.Direction,
                                    Wait = testItem.Wait,
                                    AutoReply = testItem.AutoReply,
                                    DataItemContent = testItem.DataItemContent,
                                    ReplyStream = testItem.ReplyStream,
                                    ReplyFunction = testItem.ReplyFunction,
                                    ReplyDataItemContent = testItem.ReplyDataItemContent
                                };
                                eapTestBatch.TestItems.Add(eapTestItem);
                            }
                        }

                        eapTestCase.EAPTestTemplate.TestBatches.Add(eapTestBatch);
                    }

                    if (item.TestItemId != 0 && !string.IsNullOrEmpty(item.TestItemName))
                    {
                        TestItem testItem = DataFactory.Current.TestItem.GetById(item.TestItemId);
                        EAPTestItem eapTestItem = new EAPTestItem()
                        {
                            SerialNumber = item.SerialNumber,
                            MessageName = testItem.MessageName,
                            Description = testItem.Description,
                            Stream = testItem.Stream,
                            Function = testItem.Function,
                            Direction = testItem.Direction,
                            Wait = testItem.Wait,
                            AutoReply = testItem.AutoReply,
                            DataItemContent = testItem.DataItemContent,
                            ReplyStream = testItem.ReplyStream,
                            ReplyFunction = testItem.ReplyFunction,
                            ReplyDataItemContent = testItem.ReplyDataItemContent
                        };
                        eapTestCase.EAPTestTemplate.TestItems.Add(eapTestItem);
                    }
                }

                var testCaseId = Guid.NewGuid().ToString();
                eapTestCase.EAPTestTemplate.TestItems=eapTestCase.EAPTestTemplate.TestItems.OrderBy(x => x.SerialNumber).ToList();
                foreach (var testItem in eapTestCase.EAPTestTemplate.TestItems)
                {
                    TestHistory history = new TestHistory();
                    history.TestCaseId = testCaseId;
                    history.TestCaseName = eapTestCase.TestCaseName;
                    history.TestTemplateName = eapTestCase.EAPTestTemplate.TestTempName;
                    history.SerialNumber = testItem.SerialNumber;
                    history.StartTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff");
                    history.TestItemName = testItem.MessageName;
                    history.TestItemDesc = testItem.Description;
                    history.TestResult = TestResult.OK;
                    var res = TestItemStart(testCase.EqpId, testCase.TestCaseName, testItem);
                    if (!res)
                        history.TestResult = TestResult.NG;
                    history.EndTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff");
                    DataFactory.Current.TestHistory.Insert(history);
                }

                eapTestCase.EAPTestTemplate.TestBatches=eapTestCase.EAPTestTemplate.TestBatches.OrderBy(x => x.SerialNumber).ToList();
                foreach (var testBatch in eapTestCase.EAPTestTemplate.TestBatches)
                {
                    TestHistory history = new TestHistory();
                    history.TestCaseId = testCaseId;
                    history.TestCaseName = eapTestCase.TestCaseName;
                    history.TestTemplateName = eapTestCase.EAPTestTemplate.TestTempName;
                    history.SerialNumber = testBatch.SerialNumber;
                    history.StartTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff");
                    history.TestItemName = testBatch.TestBatchName;
                    history.TestResult = TestResult.OK;

                    var res = TestBatchStart(testCase.EqpId, testCase.TestCaseName, testBatch);
                    if (!res)
                        history.TestResult = TestResult.NG;
                    history.EndTime = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss:fff");
                    DataFactory.Current.TestHistory.Insert(history);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        public static bool TestItemStart(string eqpId,string TestCaseName, EAPTestItem testItem)
        {
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "TestItemStart";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;


                EAPTestItemStart body = new EAPTestItemStart();
                body.EQUIPMENTID = eqpId;
                body.EAPTestItem = testItem;
                body.TestCaseName = TestCaseName;

                var xmlBody = XmlSerialization.SerializeOnlyBody(body);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData = ActiveMQClient.Current.SendRequestAsync<EAPTestItemStart>(xmlMessage).Result;
                if (replyData == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public static bool TestBatchStart(string eqpId, string TestCaseName, EAPTestBatch testBatch)
        {
            //先按照设定顺序排序，否则一定会测试失败
            testBatch.TestItems = testBatch.TestItems.OrderBy(x => x.SerialNumber).ToList();
            try
            {
                MQHeader header = new MQHeader();
                header.MESSAGENAME = "TestBatchStart";
                var requestMessage = new MQData();
                requestMessage.mQHeader = header;


                EAPTestBatchStart body = new EAPTestBatchStart();
                body.EQUIPMENTID = eqpId;
                body.EAPTestBatch = testBatch;
                body.TestCaseName = TestCaseName;

                var xmlBody = XmlSerialization.SerializeOnlyBody(body);
                var xmlMessage = XmlSerialization.XmlSerializeMessage<MQData>(requestMessage, xmlBody);
                var replyData = ActiveMQClient.Current.SendRequestAsync<EAPTestBatchStart>(xmlMessage).Result;
                if (replyData == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        } 

        #endregion
    }
}
