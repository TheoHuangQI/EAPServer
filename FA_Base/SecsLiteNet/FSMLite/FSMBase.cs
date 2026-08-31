using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FALogger;

namespace FSMLite
{
	public class FSMBase
	{
		public enum FSMRunMode
		{
			Basic,
			Thread
		}
		public static string ClassName = "FSMBase";
		private bool m_stopped = false;

		private Thread m_mainThread = null;

		private AutoResetEvent m_triggerEvent = new AutoResetEvent(initialState: false);

		private ConcurrentQueue<object> m_eventQueue = new ConcurrentQueue<object>();

		private Dictionary<uint, TransitionItem> m_transitionTable = new Dictionary<uint, TransitionItem>();

		private FSMRunMode m_runMode = FSMRunMode.Thread;

		public string Name { get; set; }

		public object CurrentState { get; set; }

		public object PreviousState { get; set; }

		public object ActionHost { get; set; }
		public FSMBase(object host, FSMRunMode mode, string name = "")
		{
			CurrentState = null;
			PreviousState = null;
			ActionHost = host;
			Name = name;
			m_runMode = mode;
		}

		public void DefineTransition(object currentState, object triggerEvent, object newState, FSMAction act)
		{
			TransitionItem transitionItem = new TransitionItem
			{
				CurrentState = currentState,
				TriggerEvent = triggerEvent,
				NewState = newState,
				Action = act
			};
			ushort a = Convert.ToUInt16(transitionItem.CurrentState);
			ushort b = Convert.ToUInt16(transitionItem.TriggerEvent);
			uint key = TransitionItem.MakeKey(a, b);
			m_transitionTable[key] = transitionItem;
		}

		public void UnDefineTransition()
		{
			m_transitionTable.Clear();
		}

		public virtual void Start()
		{
			if (m_triggerEvent == null)
				m_triggerEvent = new AutoResetEvent(initialState: false);
			if (m_eventQueue == null) 
				m_eventQueue = new ConcurrentQueue<object>();
			if (m_runMode == FSMRunMode.Thread)
			{
				m_stopped = false;
				if (m_mainThread == null)
				{
					m_mainThread = new Thread(__fsmThread);
					m_mainThread.Start(this);
				}
			}
		}
		public void Stop()
		{
			if (m_runMode == FSMRunMode.Thread)
			{
				m_stopped = true;
				m_triggerEvent?.Set();
				if (m_mainThread != null)
				{
					m_mainThread?.Join();
					m_mainThread = null;
				}
			}
            else
            {
				m_triggerEvent?.Dispose();
				m_triggerEvent = null;
			}
		}

		public void TriggerEvent(object ev)
		{
			if (m_runMode == FSMRunMode.Basic)
			{
				lock (this)
				{
					basicTriggerRun(ev);
					return;
				}
			}
			if (m_runMode == FSMRunMode.Thread)
			{
				m_eventQueue.Enqueue(ev);
				m_triggerEvent.Set();
			}
		}

		private void basicTriggerRun(object ev)
		{
			ushort num = Convert.ToUInt16(CurrentState);
			ushort num2 = Convert.ToUInt16(ev);
			uint key = TransitionItem.MakeKey(num, num2);
			if (!m_transitionTable.ContainsKey(key))
			{
				if(Name!= "GEM.CommunicatingEqp")
                {
					Console.WriteLine($"Undefined State/Event: [{CurrentState}({num})]/[{ev}({num2})]");
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Undefined State/Event: [{CurrentState}({num})]/[{ev}({num2})]");
			}
			else
			{
				TransitionItem transitionItem = m_transitionTable[key];
				if (Name != "GEM.CommunicatingEqp")
                {
					Console.WriteLine($"{Name}: [{ev}] {CurrentState} ==> {transitionItem.NewState}");
				}
				FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {Name}: [{ev}] {CurrentState} ==> {transitionItem.NewState}");
				PreviousState = CurrentState;
				CurrentState = transitionItem.NewState;
				transitionItem.Action?.Invoke(ActionHost);
			}
		}

		protected void fsmRun()
		{
			FALoggerClass.Inst().Info("FSM [" + Name + "] is running. ");
			do
			{
				try
				{
					m_triggerEvent.WaitOne();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
				while (m_eventQueue.Count > 0)
				{
					object result = null;
					if (m_eventQueue.TryDequeue(out result))
					{
						ushort a = Convert.ToUInt16(CurrentState);
						ushort b = Convert.ToUInt16(result);
						uint key = TransitionItem.MakeKey(a, b);
						if (!m_transitionTable.ContainsKey(key))
						{
							if (Name != "GEM.CommunicatingEqp")
							{
								Console.WriteLine($"Undefined State/Event: [{CurrentState}]/[{result}]");
							}
							FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: Undefined State/Event: [{CurrentState}]/[{result}]");
							continue;
						}
						TransitionItem transitionItem = m_transitionTable[key];
						if (Name != "GEM.CommunicatingEqp")
						{
							Console.WriteLine($"{Name}: [{result}] {CurrentState} ==> {transitionItem.NewState}");
						}
						FALoggerClass.Inst().Error($"[{ClassName}] [{MethodBase.GetCurrentMethod().Name}]: {Name}: [{result}] {CurrentState} ==> {transitionItem.NewState}");
						PreviousState = CurrentState;
						CurrentState = transitionItem.NewState;
						transitionItem.Action?.Invoke(ActionHost);
					}
				}
			}
			while (!m_stopped);
			m_triggerEvent.Dispose();
			m_triggerEvent = null;
			m_eventQueue = null;
			FALoggerClass.Inst().Info("FSM [" + Name + "] stopped. ");
		}

		private static void __fsmThread(object arg)
		{
			FSMBase fSMBase = arg as FSMBase;
			fSMBase.fsmRun();
		}
	}
}
