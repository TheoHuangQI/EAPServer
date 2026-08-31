using SecsLite;
using SecsLite.Model;

namespace SecsLite300.E40
{
	public class RecipeParamInfo
	{
		public string ParamName { get; set; }

		public SecsItem ParamValue { get; set; }

		public RecipeParamInfo(string name, SecsItem val = null)
		{
			ParamName = name;
			ParamValue = val;
		}
	}
}
