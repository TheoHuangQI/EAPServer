using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace EAP.HC.Core
{
    public static class JsonUtils
    {

        
        public static string Serialization(object msg)
        {
            return JsonConvert.SerializeObject(msg);
        }

        public static string Serialization(JObject obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
        

        /// <summary>
        /// 实体类转xml
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string SerializeToXML(Object obj)
        {
            string outXML = string.Empty;
            if (obj == null)
                return outXML;
            XmlSerializer xs = new XmlSerializer(obj.GetType(), new XmlRootAttribute("xml"));
            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces(new XmlQualifiedName[] {
                                                 new XmlQualifiedName(string.Empty, string.Empty)});



            var ms = new MemoryStream();
            using (XmlWriter xmlWriter = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true }))//使用UTF8Encoding
            {
                XmlSerializer xz = new XmlSerializer(obj.GetType(), new XmlRootAttribute("MESSAGE"));
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add(string.Empty, string.Empty);//去掉xmlns属性
                xz.Serialize(xmlWriter, obj, ns);
                outXML = Encoding.UTF8.GetString(ms.ToArray());//得到xml，不含BOM
            }
            return outXML;
        }

        /// <summary>
        /// xml转实体类
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strXML"></param>
        /// <returns></returns>
        public static T DESerializer<T>(string strXML) where T : class
        {
            try
            {
                using (StringReader sr = new StringReader(strXML))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    return serializer.Deserialize(sr) as T;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
