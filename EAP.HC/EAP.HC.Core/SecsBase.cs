using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class SecsBase
    {
        private  static Dictionary<Type, object> instanceDic=new Dictionary<Type, object>();
        public static void ReciveSecsMessage(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            // 加载DLL
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            object instance;
            if (!instanceDic.ContainsKey(type))
            {
                instance = Activator.CreateInstance(type);
                instanceDic.Add(type, instance);
            }
            // 创建实例
            instance = instanceDic[type];

             // 调用方法
             MethodInfo method = type.GetMethod("SecsMsgHandler");

            if (method != null)
            {
                try
                {
                    Arguments args = new Arguments()
                    {
                        Header = new Dictionary<string, object>(),
                        SecsData = primaryMessage
                    };
                    args.Header[CItems.EqpId] = context.EquipmentID;
                    args.Header[CItems.EqpType] = context.EqpType;
                    method.Invoke(instance, new object[] { args }); // 调用无参数的方法
                                                                                       // 如果有参数，可以这样调用：method.Invoke(instance, new object[] { arg1, arg2 });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Method not found.");
                }

            }
            else
            {
                Console.WriteLine("Method not found.");
            }
        }

        public static void ConnectionChange(ContextObject context, ConnectionState state)
        {
            // 加载DLL
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            object instance;
            if (!instanceDic.ContainsKey(type))
            {
                instance = Activator.CreateInstance(type);
                instanceDic.Add(type, instance);
            }
            // 创建实例
            instance = instanceDic[type];

            // 调用方法
            MethodInfo method = type.GetMethod("ConnectionChange");

            if (method != null)
            {
                try
                {
                    Arguments args = new Arguments()
                    {
                        Header = new Dictionary<string, object>(),
                    };
                    args.Header[CItems.EqpId] = context.EquipmentID;
                    args.Header[CItems.EqpType] = context.EqpType;
                    args.Header[CItems.ConnectionState] = state;
                    method.Invoke(instance, new object[] { args }); // 调用无参数的方法

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Method not found.");
                }

            }
            else
            {
                Console.WriteLine("Method not found.");
            }
        }
    }
}
