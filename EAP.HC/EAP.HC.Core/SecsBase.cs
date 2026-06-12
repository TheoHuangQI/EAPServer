using EAP.HC.Core.Entities.MES;
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
        private  static Dictionary<string, object> instanceDic=new Dictionary<string, object>();

        public static void ReciveSecsMessage(ContextObject context,Arguments args ,string messageName)
        {
            if (context == null) 
                return;

            // 动态加载 DLL：ReciveSecsMessage 方法会根据设备的类型（context.EqpType，例如 KS_CM300）动态加载对应的设备通信实现 DLL，例如 EAP.EC.KS_CM300.dll。
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            string instanceKey = $"{context.EqpType}:{context.EquipmentID}";
            object instance;
            lock (instanceDic)
            {
                if (!instanceDic.ContainsKey(instanceKey))
                {
                    instance = Activator.CreateInstance(type);
                    instanceDic.Add(instanceKey, instance);
                }
            }
            // 创建实例
            instance = instanceDic[instanceKey];

             // 反射调用方法：它会在加载的 DLL 中寻找一个与设备类型同名的类（例如 KS_CM300），然后通过反射调用与 messageName 参数同名的方法（例如 ConnectionChange）。
             MethodInfo method = type.GetMethod(messageName);

            if (method != null)
            {
                try
                {
                    if (messageName == "ConnectionChange")
                    {
                        method.Invoke(instance, new object[] { args });
                        return;
                    }

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        method.Invoke(instance, new object[] { args });   // 调用无参数的方法
                    });

                  
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
        public static T ReciveSecsMessage<T>(ContextObject context, Arguments args, string messageName)
        {
            if (context == null)
                return default;
            // 加载DLL
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            string instanceKey = $"{context.EqpType}:{context.EquipmentID}";
            object instance;
            lock (instanceDic)
            {
                if (!instanceDic.ContainsKey(instanceKey))
                {
                    instance = Activator.CreateInstance(type);
                    instanceDic.Add(instanceKey, instance);
                }
            }
            // 创建实例
            instance = instanceDic[instanceKey];

            // 调用方法
            MethodInfo method = type.GetMethod(messageName);

            if (method != null)
            {
                try
                {
                   var res= method.Invoke(instance, new object[] { args }); // 调用无参数的方法
                   return (T)res;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Method not found.");
                    return default(T);
                }
            }
            else
            {
                Console.WriteLine("Method not found.");
                return default(T);
            }
        }
        public static List<string> GetPPIDList(ContextObject context)
        {
            // 加载DLL
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            string instanceKey = $"{context.EqpType}:{context.EquipmentID}";
            object instance;
            lock (instanceDic)
            {
                if (!instanceDic.ContainsKey(instanceKey))
                {
                    instance = Activator.CreateInstance(type);
                    instanceDic.Add(instanceKey, instance);
                }
            }
            // 创建实例
            instance = instanceDic[instanceKey];

            // 调用方法
            MethodInfo method = type.GetMethod("GetPPIDList");

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
                    object res=method.Invoke(instance, new object[] { args });
                    return res as List<string>;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Method not found.");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Method not found.");
                return null;
            }
        }

        public static bool StartTest(ContextObject context,EAPTestItem testItem)
        {
            // 加载DLL
            Assembly assembly = Assembly.LoadFrom($"{EAPContext.RootPath}\\EAP.EC.{context.EqpType}.dll");

            // 获取类型
            Type type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}");
            string instanceKey = $"{context.EqpType}:{context.EquipmentID}";
            object instance;
            lock (instanceDic)
            {
                if (!instanceDic.ContainsKey(instanceKey))
                {
                    instance = Activator.CreateInstance(type);
                    instanceDic.Add(instanceKey, instance);
                }
            }

            // 创建实例
            instance = instanceDic[instanceKey];

            // 调用方法
            MethodInfo method = type.GetMethod("StartTest");

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
                    args.Header[CItems.TestItem] = testItem;
                    object res = method.Invoke(instance, new object[] { args });
                    return (bool)res;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Method not found.");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Method not found.");
                return false;
            }
        }
    }
}
