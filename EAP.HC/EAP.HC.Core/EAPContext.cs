using Apache.NMS.ActiveMQ.Commands;
using EAP.HC.Core.Entities;
using EAP.HC.Core.Entities.MES;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EAP.HC.Core
{
    public class EAPContext
    {
        public static string RootPath= AppDomain.CurrentDomain.BaseDirectory;
        public static object Configuration { get; set; }
        public static string ServerName { get; set; }
        public static List<ConnectionConfigure> EqpConfigures { get; set; }

        public static System.Collections.Concurrent.ConcurrentBag<ContextObject> contexts { get; set; }
        public static bool Initialization()
        {
            //LoadLibrary();
            string path = Path.Combine(EAPContext.RootPath, $"Configuration\\{EAPContext.ServerName}.json");
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                EqpConfigures = JsonConvert.DeserializeObject<List<ConnectionConfigure>>(text);
                contexts = new System.Collections.Concurrent.ConcurrentBag<ContextObject>();
                foreach(var eqpconfig in EqpConfigures)
                {
                    eqpconfig.Validate();
                    if (!AppCache.Current.EqpInfos.ContainsKey(eqpconfig.Name))
                    {
                        AppCache.Current.EqpInfos.Add(eqpconfig.Name, new EqpInfo()
                        {
                            IsInitial = false,
                            EQPID = eqpconfig.Name,
                            EQPType = eqpconfig.EQPType,
                            EqpStatus = EQPStatus.IDLE,
                            ControlStatus = ControlState.EQPOFFLINE,
                            ConnectStatus = ConnectStatus.DISCONNECTED,
                            MESConnectStatus = MESConnectStatus.DISCONNECTED,
                            Chamber = eqpconfig.Chamber,
                            LoadPort = eqpconfig.LoadPort,
                            PortInfo = new Dictionary<int, PortInfo>()
                        });
                        for(int i=1;i<= AppCache.Current.EqpInfos[eqpconfig.Name].LoadPort; i++)
                        {
                            AppCache.Current.EqpInfos[eqpconfig.Name].PortInfo.Add(i, new PortInfo()
                            {
                                PortNo = i,
                                CarrierId = string.Empty,
                                PortStatus = PortStatus.Idle,
                                BatchId = string.Empty,
                                LotIds = new List<string>()
                            });
                        }
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }


        public static void LoadLibrary()
        {
            try
            {
                string path = Path.Combine(EAPContext.RootPath, $"SECSLibrary.xml");
                if (!File.Exists(path))
                {
                    return;
                }
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(path);
                XmlNode xmlNode = xmlDocument.SelectSingleNode("SECSLibrary");
                XmlNodeList xmlNodeList = xmlNode.SelectNodes("SECSMessage");
                foreach (XmlNode node in xmlNodeList)
                {
                    SECSTransaction transaction = new SECSTransaction();
                    transaction.MessageName = node.SelectSingleNode("MessageName").InnerText;
                    transaction.Description = node.SelectSingleNode("Description").InnerText;
                    transaction.Stream = int.Parse(node.SelectSingleNode("Stream").InnerText);
                    transaction.Function = int.Parse(node.SelectSingleNode("Function").InnerText);
                    transaction.Direction = node.SelectSingleNode("Direction").InnerText;
                    transaction.Wait = bool.Parse(node.SelectSingleNode("Wait").InnerText);
                    transaction.AutoReply = bool.Parse(node.SelectSingleNode("AutoReply").InnerText);
                    transaction.DataItemContent = node.SelectSingleNode("DataItem").InnerXml;

                    SecsMessage message = new SecsMessage((byte)transaction.Stream, (byte)transaction.Function, transaction.AutoReply);
                    XmlNode dataNode = node.SelectSingleNode("DataItem");
                    //如果子节点不为空且是Host->EQP 的消息
                    if (dataNode.ChildNodes.Count > 0 && (transaction.Direction== "H->E" || transaction.Direction== "E<-H"))
                    {
                        message.SecsItem = GetMessageSecsItem(dataNode.ChildNodes[0]);
                    }
                    transaction.message = message;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static Item GetMessageSecsItem(XmlNode node)
        {
            try
            {
                // 快速筛选 ChildNodes 中的 Element 节点数量
                var elementNodes = node.ChildNodes.OfType<XmlElement>();
                Item secsItem = GetSecsItem(node.Name, node.InnerText, elementNodes.Count());
                if (elementNodes.Count() > 0)
                {
                    int i = 0;
                    foreach (XmlNode cnode in elementNodes)
                    {
                        secsItem.Items[i] = GetMessageSecsItem(cnode);
                        i++;
                    }
                }
                return secsItem;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        public static Item GetSecsItem(string Name,string Content,int ChildCount)
        {
            try
            {
                switch (Name)
                {
                    case "L":
                        Item[] Items = new Item[ChildCount];
                        return Item.L(Items);
                    case "A":
                        return Item.A(Content);
                    case "JIS8":
                        return Item.J(Content);
                    case "B":
                        return Item.B(byte.Parse(Content));
                    case "F4":
                        return Item.F4(float.Parse(Content));
                    case "F8":
                        return Item.F8(double.Parse(Content));
                    case "I1":
                        return Item.I1(sbyte.Parse(Content));
                    case "I2":
                        return Item.I2(short.Parse(Content));
                    case "I4":
                        return Item.I4(int.Parse(Content));
                    case "I8":
                        return Item.I8(long.Parse(Content));
                    case "U1":
                        return Item.U1(byte.Parse(Content));
                    case "U2":
                        return Item.U2(ushort.Parse(Content));
                    case "U4":
                        return Item.U4(uint.Parse(Content));
                    case "U8":
                        return Item.U8(ulong.Parse(Content));
                    case "Boolean":
                        return Item.Boolean(bool.Parse(Content));
                    default:
                        return null;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
            
        }
        public static bool SaveLibraryToXml(SECSLibrary library)
        {
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlNode xmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "SECSLibrary", "");
                xmlDocument.AppendChild(xmlNode);
                foreach (SECSTransaction transaction in library.SECSTransactions)
                {
                    XmlNode MsgXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "SECSMessage", "");
                    XmlNode MsgNameXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "MessageName","");
                    MsgNameXmlNode.InnerText = transaction.MessageName;
                    XmlNode DescXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "Description","");
                    DescXmlNode.InnerText = transaction.Description;
                    XmlNode StreamXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "Stream","");
                    StreamXmlNode.InnerText = transaction.Stream.ToString();
                    XmlNode FuncXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "Function", "");
                    FuncXmlNode.InnerText = transaction.Function.ToString();
                    XmlNode DireXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "Direction", "");
                    DireXmlNode.InnerText = transaction.Direction;
                    XmlNode WaitXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "Wait","");
                    WaitXmlNode.InnerText = transaction.Wait.ToString().ToLower();
                    XmlNode AutoReplyXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "AutoReply", "");
                    AutoReplyXmlNode.InnerText = transaction.AutoReply.ToString().ToLower();
                    XmlNode DataXmlNode = xmlDocument.CreateNode(XmlNodeType.Element, "DataItem","");
                    DataXmlNode.InnerXml = transaction.DataItemContent;
                    MsgXmlNode.AppendChild(MsgNameXmlNode);
                    MsgXmlNode.AppendChild(DescXmlNode);
                    MsgXmlNode.AppendChild(StreamXmlNode);
                    MsgXmlNode.AppendChild(FuncXmlNode);
                    MsgXmlNode.AppendChild(DireXmlNode);
                    MsgXmlNode.AppendChild(WaitXmlNode);
                    MsgXmlNode.AppendChild(AutoReplyXmlNode);
                    MsgXmlNode.AppendChild(DataXmlNode);
                    xmlNode.AppendChild(MsgXmlNode);
                }

                string path = Path.Combine(EAPContext.RootPath, $"SECSLibrary.xml");
                xmlDocument.Save(path);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }


        }
    }
}
