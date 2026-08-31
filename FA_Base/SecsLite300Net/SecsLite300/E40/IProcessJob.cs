using System.Collections.Generic;
using SecsLite;
using SecsLite300.E90;

namespace SecsLite300.E40
{
	public interface IProcessJob
	{
		string ProcessJobID { get; }

		bool PRProcessStart { get; }

		MaterialFormat PRMtlType { get; }

		List<MaterialInfo> PRMtlNameList { get; }

		ProcessJobState PRJobState { get; }

		ProcessRecipeMethod PRRecipeMethod { get; }

		string RecipeID { get; }

		uint[] PauseEventArray { get; }

		List<ISubstrate> Substrates { get; }

		string ControlJobID { get; }
		List<RecipeParamInfo> RecVariableList { get; }

		PJResultCode PRJobSetup();

		PJResultCode PRJobSetupDone();

		PJResultCode PRJobDone();

		PJResultCode PRJobProcessed();

		PJResultCode PRJobResumed();
		PJResultCode PRJobAbort();
		PJResultCode PRJobAbortDone();
		PJResultCode PRJobStop();
		PJResultCode PRJobStopDone();
		PJResultCode PRJobPause();
		PJResultCode PRJobPauseDone();
	}
}
