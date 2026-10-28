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
        // Values are persisted in RUN_HISTORY.STATE and must stay aligned with the UI.
        Start = 0,
        Running = 1,
        End = 2,
        Error = 3
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
