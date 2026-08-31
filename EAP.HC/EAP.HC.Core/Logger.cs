using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using log4net.Config;

namespace EAPManage.Utils
{
    public enum LogType
    {
        NOTUSE,
        ERROR,
        WARNING,
        DEBUG,
        INFO
    }

    public class Logger
    {
        private static readonly bool _logConfigured = ConfigureLog4Net();
        private static readonly log4net.ILog logInfo = log4net.LogManager.GetLogger("loginfo");
        private static readonly log4net.ILog logDebug = log4net.LogManager.GetLogger("logdebug");
        private static readonly log4net.ILog logWarn = log4net.LogManager.GetLogger("logwarn");
        private static readonly log4net.ILog logError = log4net.LogManager.GetLogger("logerror");

        private static Logger __logger;

        public static Logger Inst()
        {
            if (__logger == null)
            {
                __logger = new Logger();
            }
            return __logger;
        }

        public Logger()
        {
            _ = _logConfigured;
        }

        private static bool ConfigureLog4Net()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
                if (!File.Exists(configPath))
                {
                    return false;
                }

                EnsureLogDirectory();
                XmlConfigurator.Configure(
                    log4net.LogManager.GetRepository(Assembly.GetExecutingAssembly()),
                    new FileInfo(configPath));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureLogDirectory()
        {
            try
            {
                Directory.CreateDirectory(@"E:\kingsemi_log\fa\FALog");
            }
            catch
            {
            }
        }

        public void InitialfileWtrierTimer()
        {
        }

        public void Debug(string logStr)
        {
            if (logDebug.IsInfoEnabled)
            {
                logDebug.Debug(logStr.Trim());
            }
        }

        public void Error(string logStr)
        {
            if (logError.IsInfoEnabled)
            {
                logError.Error(logStr.Trim());
            }
        }

        public void Info(string logStr)
        {
            if (logInfo.IsInfoEnabled)
            {
                logInfo.Error(logStr.Trim());
            }
        }

        public void Warn(string logStr)
        {
            if (logWarn.IsInfoEnabled)
            {
                logWarn.Error(logStr.Trim());
            }
        }

        public void WriteFile(string funcName, LogType logType, string Desc)
        {
            StringBuilder strLogMsg = new StringBuilder();
            strLogMsg.Append("   ");
            strLogMsg.Append(funcName);
            strLogMsg.Append("   ");
            strLogMsg.Append(Desc);
            strLogMsg.AppendLine();
            Info(strLogMsg.ToString());
            strLogMsg.Remove(0, strLogMsg.Length);
            strLogMsg = null;
        }

        public void WriteBaseToFile(string funcName, string Desc = null)
        {
            StringBuilder strLogMsg = new StringBuilder();
            strLogMsg.Append("   Base    ");
            strLogMsg.Append(funcName);
            strLogMsg.Append("   ");
            strLogMsg.Append(Desc);
            strLogMsg.AppendLine();
            Info(strLogMsg.ToString());
            strLogMsg.Remove(0, strLogMsg.Length);
            strLogMsg = null;
        }

        public void WriteExceptionLog(System.Exception ex, string personInfo)
        {
            Error(ex.Message + "\n" + ex.StackTrace);
        }
    }
}
