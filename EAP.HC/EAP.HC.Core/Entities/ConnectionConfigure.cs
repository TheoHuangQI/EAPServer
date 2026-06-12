﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public class ConnectionConfigure
    {
        public string Name { get; set; } = string.Empty;

        public int DeviceID { get; set; }

        public ConnectionType ConnectionType { get; set; }

        public ConnectionMode ConnectionMode { get; set; }

        public string LocalIP { get; set; } = string.Empty;

        public int LocalPort { get; set; }

        public string RemoteIP { get; set; } = string.Empty;

        public int RemotePort { get; set; }

        public string EQPType { get; set; } = string.Empty;

        /// <summary>
        /// SECS/GEM T3 Timeout（秒）
        /// </summary>
        public int T3 { get; set; } = 45;

        /// <summary>
        /// SECS/GEM T5 Timeout（秒）
        /// </summary>
        public int T5 { get; set; } = 10;

        /// <summary>
        /// SECS/GEM T6 Timeout（秒）
        /// </summary>
        public int T6 { get; set; } = 5;

        /// <summary>
        /// SECS/GEM T7 Timeout（秒）
        /// </summary>
        public int T7 { get; set; } = 10;

        /// <summary>
        /// SECS/GEM T8 Timeout（秒）
        /// </summary>
        public int T8 { get; set; } = 5;

        public string Chamber { get; set; } = string.Empty;

        public int LoadPort { get; set; }

        /// <summary>
        /// 心跳检测周期（秒）
        /// </summary>
        public int? HeartbeatInterval { get; set; } = 30;

        /// <summary>
        /// 心跳连续失败最大次数
        /// </summary>
        public int? HeartbeatMaxFailures { get; set; } = 3;

        /// <summary>
        /// 配置合法性检查
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Name不能为空");

            if (DeviceID < 0)
                throw new ArgumentException("DeviceID必须大于或等于0");

            if (string.IsNullOrWhiteSpace(LocalIP))
                throw new ArgumentException("LocalIP不能为空");

            if (!IPAddress.TryParse(LocalIP, out _))
                throw new ArgumentException($"LocalIP格式错误: {LocalIP}");

            if (LocalPort < 1 || LocalPort > 65535)
                throw new ArgumentException($"LocalPort无效: {LocalPort}");

            if (!string.IsNullOrWhiteSpace(RemoteIP) &&
                !IPAddress.TryParse(RemoteIP, out _))
                throw new ArgumentException($"RemoteIP格式错误: {RemoteIP}");

            if (RemotePort != 0 &&
                (RemotePort < 1 || RemotePort > 65535))
                throw new ArgumentException($"RemotePort无效: {RemotePort}");

            if (T3 <= 0)
                throw new ArgumentException("T3必须大于0");

            if (T5 <= 0)
                throw new ArgumentException("T5必须大于0");

            if (T6 <= 0)
                throw new ArgumentException("T6必须大于0");

            if (T7 <= 0)
                throw new ArgumentException("T7必须大于0");

            if (T8 <= 0)
                throw new ArgumentException("T8必须大于0");

            if (HeartbeatInterval.HasValue &&
                HeartbeatInterval.Value <= 0)
                throw new ArgumentException("HeartbeatInterval必须大于0");

            if (HeartbeatMaxFailures.HasValue &&
                HeartbeatMaxFailures.Value <= 0)
                throw new ArgumentException("HeartbeatMaxFailures必须大于0");
        }
    }
}