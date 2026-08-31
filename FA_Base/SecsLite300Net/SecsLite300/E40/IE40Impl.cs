using SecsLite;

namespace SecsLite300.E40
{
	internal interface IE40Impl
	{
		ERRCODE OnProcessJobCreate(IProcessJob pj, ref string errText);

		ERRCODE OnProcessJobSetup(IProcessJob pj, ref string errText);

		ERRCODE OnProcessJobAbort(IProcessJob pj, ref string errText);

		ERRCODE OnProcessJobCancel(IProcessJob pj,ref string errText);

		ERRCODE OnProcessJobStop(IProcessJob pj, ref string errText);

		ERRCODE OnProcessJobResume(IProcessJob pj, ref string errText);

		ERRCODE OnProcessJobPause(IProcessJob pj,ref string errText);

		void OnProcessJobDelete(IProcessJob pj);

		void OnProcessJobProcessing(IProcessJob pj);

		bool OnProcessJobStart(IProcessJob pj);

		void OnProcessJobCarrierArrived(IProcessJob pj);

		void OnProcessJobWaitForStart(IProcessJob pj);

		void OnProcessJobStatusChange(IProcessJob pj);
	}
}
