using SecsLite;
using SecsLite.Model;

namespace SecsLite300.E39
{
	public struct GetAttrFilter
	{
		public string AttrId;

		public ATTRRELN Relation;

		public SecsItem AttrData;
	}
}
