using System;
using System.Collections.Generic;

namespace SecsLite.Model
{
	public class DataVariableSet
	{
		private Dictionary<uint, object> m_dvDict = new Dictionary<uint, object>();

		public DataVariableSet()
		{
		}

		public DataVariableSet(uint vid, object val)
		{
			if (val == null)
			{
				throw new Exception("value can't be null");
			}
			m_dvDict[vid] = val;
		}

		public void Add(uint vid, object val)
		{
			m_dvDict[vid] = val;
		}

		public virtual object GetValue(uint vid)
		{
			if (!m_dvDict.ContainsKey(vid))
			{
				return null;
			}
			return m_dvDict[vid];
		}
	}
}
