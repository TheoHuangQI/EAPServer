using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    /// <summary>
    /// 设备状态
    /// </summary>
    public enum EQPStatus
    {
        INIT,
        IDLE,
        SETUP,
        READY,
        EXECUTING,
        PAUSE,
        ERROR,
        DOWN,
        RUN,
        PM,
        PURGE,
        OFF
    }

    /// <summary>
    /// 设备连接状态
    /// </summary>
    public enum ConnectStatus
    {
        CONNECTED,
        DISCONNECTED
    }

    /// <summary>
    /// MES 连接状态
    /// </summary>
    public enum MESConnectStatus
    {
        CONNECTED,
        DISCONNECTED
    }

    /// <summary>
    /// 设备控制状态
    /// </summary>
    public enum ControlState
    {
        UNKNOWN = 0,
        EQPOFFLINE = 1,
        OFFLINEATTEMPTINGTOONLINE = 2,
        HOSTOFFLINE = 3,
        LOCAL = 4,
        REMOTE = 5,
    }

    public enum PortStatus
    {
        WAITPPSELECT,
        PPSELECTED,
        CREATWAFER,
        CREATWAFERFAIL,
        PPSELECTFAIL,
        STARTED,
        STARTFAIL,
        RUN,
        END
    }

    /// <summary>
    /// Lot状态
    /// </summary>
    public enum LotState
    {
        WaitTrackIn,
        TrackIn,
        PPSELECTED,
        CREATWAFER,
        LotStart,
        RUN,
        END,
        TrackOut,
        HOLD
    }

    public enum ConnectionType
    {
        SECSI,
        HSMS
    }

    public enum ConnectionMode
    {
        ACTIVE,
        PASSIVE
    }
}
