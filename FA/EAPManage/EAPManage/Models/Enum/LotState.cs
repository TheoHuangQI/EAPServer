namespace EAPManage.Models.Enum
{
    public enum LotState
    {
        Idle,
        WaitTrackIn,
        TrackIn,
        LotStart,
        LotEnd,
        TrackOut
    }

    public enum LotRunState
    {
        Start,
        Error,
        End
    }

    public enum TestType
    {
        EQUIPMENT,
        LOT
    }

    public enum TestResult
    {
        NG,
        OK
    }

    public enum CarrierUseState
    {
        IDLE=1,
        USE=2
    }
}
