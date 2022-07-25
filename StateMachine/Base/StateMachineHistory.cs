using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils.StateMachine
{
	public class StateMachineHistory : IHistory
	{
		public event Action<string> AddedToHistoryEvent;

		private HashSet<string> history = new HashSet<string>();

		public void Add(string id)
		{
			if (!history.Contains(id))
			{
				history.Add(id);
				AddedToHistoryEvent?.Invoke(id);
			}
		}

		public void Add(params string[] ids)
		{
			foreach (string nodeID in ids)
			{
				Add(nodeID);
			}
		}

		public bool Contains(params string[] ids)
		{
			return ids.All((id) => history.Contains(id));
		}
	}
}
