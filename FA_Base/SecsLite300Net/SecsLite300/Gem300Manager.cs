using System;
using SecsLite300.E116;
using SecsLite300.E40;
using SecsLite300.E87;
using SecsLite300.E90;
using SecsLite300.E94;

namespace SecsLite300
{
	internal static class Gem300Manager
	{
		private static E40Manager m_e40 = new E40Manager();

		private static E94Manager m_e94 = new E94Manager();

		private static E87Manager m_e87 = new E87Manager();

		private static E90Manager m_e90 = new E90Manager();

		private static E116Manager m_e116 = new E116Manager();

		private static Gem300Equipment m_eq = null;

		public static E40Manager E40 => m_e40;

		public static E94Manager E94 => m_e94;

		public static E87Manager E87 => m_e87;

		public static E90Manager E90 => m_e90;

		public static E116Manager E116 => m_e116;

		public static Gem300Equipment EquipmentObject => m_eq;

		public static void Init(Gem300Equipment eqObject)
		{
			m_eq = eqObject;
			loadModules(eqObject);
		}

		public static void Uninit(Gem300Equipment eqObject)
		{
			unloadModules(eqObject);
		}

		private static void loadModules(Gem300Equipment eqObject)
		{
			if (!m_e40.Initialize(eqObject))
			{
				throw new Exception("Load E40 Module failed.");
			}
			if (!m_e94.Initialize(eqObject))
			{
				throw new Exception("Load E94 Module failed.");
			}
			if (!m_e87.Initialize(eqObject))
			{
				throw new Exception("Load E87 Module failed.");
			}
			if (!m_e90.Initialize(eqObject))
			{
				throw new Exception("Load E90 Module failed.");
			}
		}

		private static void unloadModules(Gem300Equipment eqObject)
		{
			m_e40.Uninitialize(eqObject);
			m_e94.Uninitialize(eqObject);
			m_e87.Uninitialize(eqObject);
			m_e90.Uninitialize(eqObject);
		}

		public static bool IsPJobMaterialPresent(ProcessJob pjOb)
		{
			foreach (MaterialInfo pRMtlName in pjOb.PRMtlNameList)
			{
				Carrier carrier = E87.FindCarrier(pRMtlName.CarrierID);
				if (carrier == null)
				{
					return false;
				}
				if (carrier.IDStatus != CarrierIDStatus.VerificationOK || carrier.SlotMapStatus != CarrierSlotMapStatus.VerificationOK)
				{
					return false;
				}
			}
			return true;
		}
	}
}
