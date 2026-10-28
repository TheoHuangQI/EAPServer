using EAP.HC.Core.Entities.MES;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EAPManage.Utils;

namespace EAP.HC.Core
{
    public class SecsBase
    {
        private  static Dictionary<string, object> instanceDic=new Dictionary<string, object>();

        public static void ReciveSecsMessage(ContextObject context,Arguments args ,string messageName)
        {
            // Keep the receive loop concurrent. Each detached task has its own exception observer.
            // Connection changes start inline so the version snapshot is updated before returning.
            if (messageName == "ConnectionChange")
                _ = ObserveMessageAsync(context, args, messageName);
            else
                _ = Task.Run(() => ObserveMessageAsync(context, args, messageName));
        }

        private static async Task ObserveMessageAsync(ContextObject context, Arguments args, string messageName)
        {
            try
            {
                await ReciveSecsMessageAsync(context, args, messageName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                args?.SetResultNG(ex.Message);
                Logger.Inst().Error($"SECS dispatch failed TraceId={args?.TraceId} EqpId={context?.EquipmentID} "
                    + $"Method={messageName} Exception={ex}");
            }
        }

        public static Task ReciveSecsMessageAsync(ContextObject context, Arguments args, string messageName)
        {
            ArgumentNullException.ThrowIfNull(args);
            return SecsExecution.RunAsync(args, messageName, async () =>
            {
                if (context == null)
                    throw new InvalidOperationException("SECS dispatch context is unavailable.");
                var assembly = Assembly.LoadFrom(Path.Combine(EAPContext.RootPath, $"EAP.EC.{context.EqpType}.dll"));
                var type = assembly.GetType($"EAP.EC.{context.EqpType}.{context.EqpType}", throwOnError: true);
                var instanceKey = $"{context.EqpType}:{context.EquipmentID}";
                object instance;
                lock (instanceDic)
                {
                    if (!instanceDic.TryGetValue(instanceKey, out instance))
                    {
                        instance = Activator.CreateInstance(type);
                        instanceDic.Add(instanceKey, instance);
                    }
                }
                await MessageInvocation.InvokeAsync(instance, messageName, args).ConfigureAwait(false);
                if (string.IsNullOrEmpty(args.GetResultCode()))
                    args.SetResultOK();
            });
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
