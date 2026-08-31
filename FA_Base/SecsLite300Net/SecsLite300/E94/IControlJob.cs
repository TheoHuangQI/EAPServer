using System.Collections.Generic;
using SecsLite300.E40;

namespace SecsLite300.E94
{
	public interface IControlJob
	{
		string CJID { get; }

		ControlJobState CJobState { get; }

		CJProcessOrderMgmt ProcessOrderMgmt { get; }

		bool StartMethod { get; }

		List<string> CarrierInputSpec { get; }

		List<ProcessCtrl> ProcessingCtrlSpec { get; }

		List<MtrlOutSpec> MtrlOutSpecList { get; }

		uint[] PauseEventArray { get; }

		List<IProcessJob> CurrentPRJob { get; }

		string DataCollectionPlan { get; }

		void Stop();
		void Resume();
		void Pause();
		void Abort();
		void CJDone();
		void Select();
	}
}
