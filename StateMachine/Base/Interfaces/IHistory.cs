using System;

namespace SpaxUtils.StateMachines
{
	public interface IHistory
	{
		event Action<string> AddedToHistoryEvent;

		void Add(string id);

		void Add(params string[] ids);

		bool Contains(params string[] ids);
	}
}
