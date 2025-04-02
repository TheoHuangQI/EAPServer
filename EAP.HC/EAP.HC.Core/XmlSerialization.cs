using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace EAP.HC.Core
{
    public static class XmlSerialization
    {
        private static ConcurrentDictionary<string, XmlSerializer> _cache = new ConcurrentDictionary<string, XmlSerializer>();

        static XmlSerializer GetSerializer<T>()
        {
            var rootElementName = typeof(T).FullName;
            if (!_cache.TryGetValue(rootElementName, out var serializer))
            {
                serializer = new XmlSerializer(typeof(T));
                _cache.TryAdd(rootElementName, serializer);
            }
            return serializer;
        }
        static XmlSerializer GetSerializer(object o)
        {
            var rootElementName = o.GetType().FullName;
            if (!_cache.TryGetValue(rootElementName, out var serializer))
            {
                serializer = new XmlSerializer(o.GetType());
                _cache.TryAdd(rootElementName, serializer);
            }
            return serializer;
        }

        private static void XmlSerializeInternal(Stream stream, object o, Encoding encoding)
        {
            if (o == null)
                throw new ArgumentNullException("o");
            if (encoding == null)
                throw new ArgumentNullException("encoding");

            var serializer = GetSerializer(o);
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.NewLineChars = "\r\n";
            settings.Encoding = encoding;
            settings.IndentChars = "    ";

            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                serializer.Serialize(writer, o, ns);
                writer.Close();
            }
        }

        /// <summary>
        /// 将一个对象序列化为XML字符串
        /// </summary>
        /// <param name="o">要序列化的对象</param>
        /// <param name="encoding">编码方式</param>
        /// <returns>序列化产生的XML字符串</returns>
        public static string XmlSerialize(object o)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                XmlSerializeInternal(stream, o, Encoding.UTF8);

                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
        /// <summary> 
        /// 反序列化Header或者Return,暂时不用该方法
        /// </summary> 
        public static void DeserializeHeaderOrReturn<T>(string s, string messageRoot, string TNodeName, out T header) where T : class
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml(s);
            var temp = xd.SelectSingleNode(messageRoot).SelectSingleNode(TNodeName);
            header = ConvertNode<T>(temp);

        }

        /// <summary>
        /// 从XML字符串中反序列化对象
        /// </summary>
        /// <typeparam name="T">结果对象类型</typeparam>
        /// <param name="s">包含对象的XML字符串</param>
        /// <param name="encoding">编码方式</param>
        /// <returns>反序列化得到的对象</returns>
        public static T XmlDeserialize<T>(string s)
        {
            if (string.IsNullOrEmpty(s))
                throw new ArgumentNullException("s");

            var mySerializer = GetSerializer<T>();
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                using (StreamReader sr = new StreamReader(ms, Encoding.UTF8))
                {
                    return (T)mySerializer.Deserialize(sr);
                }
            }
        }

        /// <summary>
        /// 序列化不包含xml定义
        /// </summary>
        /// <typeparam name="R"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static string SerializeOnlyBody<R>(R t)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var serializer = GetSerializer(t);

                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add("", "");

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = false;
                settings.NewLineChars = "\r\n";
                settings.Encoding = Encoding.UTF8;
                settings.OmitXmlDeclaration = true;

                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                {
                    serializer.Serialize(writer, t, ns);
                    writer.Close();
                    stream.Position = 0;
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary> 
        /// 反序列化Node
        /// </summary> 
        public static T DeserializeNodeObj<T>(string s, string messageRoot, string messageBody) where T : class
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml(s);

            var body = xd.SelectSingleNode(messageRoot).SelectSingleNode(messageBody);
            if (body == null)
                return default(T);
            return ConvertNode<T>(body);
        }


        private static T ConvertNode<T>(XmlNode node) where T : class
        {
            using (MemoryStream stm = new MemoryStream())
            {
                using (StreamWriter stw = new StreamWriter(stm))
                {
                    stw.Write(node.OuterXml);
                    stw.Flush();

                    stm.Position = 0;

                    var ser = GetSerializer<T>();
                    T result = (ser.Deserialize(stm) as T);

                    return result;
                }
            }
        }

        /// <summary>
        /// 序列化成R
        /// </summary>
        /// <typeparam name="R">Message类名</typeparam>
        /// <param name="obj">Message字符串</param>
        /// <param name="body">Body 字符串</param>
        /// <returns></returns>
        public static string XmlSerializeMessage<R>(R obj, string body) where R : new()
        {
            var msg = XmlSerialize(obj);
            msg = msg.Replace("<Body />", body).ToXmlFormat();
            return msg;
        }

        /// <summary>
        /// 转换成能换行的xml
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static string ToXmlFormat(this string xml)
        {
            if (string.IsNullOrEmpty(xml))
                return "";
            using (MemoryStream ms = new MemoryStream())
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                doc.Save(ms);

                using (StreamReader reader = new StreamReader(ms, Encoding.UTF8))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    return reader.ReadToEnd();
                }
            }

        }
    }
}
