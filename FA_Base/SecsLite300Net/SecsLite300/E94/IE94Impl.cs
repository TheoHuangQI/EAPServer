using SecsLite;

namespace SecsLite300.E94
{
	internal interface IE94Impl
	{
		ERRCODE OnControlJobCreate(IControlJob cj);

		ERRCODE OnControlJobAbort(IControlJob cj,ref string errText);

		ERRCODE OnControlJobCancel(IControlJob cj, ref string errText);

		ERRCODE OnControlJobPause(IControlJob cj, ref string errText);

		ERRCODE OnControlJobResume(IControlJob cj, ref string errText);

		ERRCODE OnControlJobStop(IControlJob cj, ref string errText);

		CJSelectResult OnControlJobChosen(IControlJob job, IControlJob currentJob);

		

		int OnControlJobDelete(IControlJob cj);

		int OnControlJobDone(IControlJob cj, CJCompleteType type);

		

		CJSelectionMode OnControlJobProcess(IControlJob cj);

		

		void OnControlJobSelected(IControlJob cj);

		int OnControlJobStart(IControlJob cj);

	

		CJSelectionMode OnControlJobWait(IControlJob cj);
		void OnControlJobStatusChange(IControlJob cj);
	}
}
