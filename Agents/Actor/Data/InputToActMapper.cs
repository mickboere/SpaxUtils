using System;

namespace SpaxUtils
{
	/// <summary>
	/// Instanced helper class that uses an <see cref="InputToActMapping"/> to map boolean input to actor acts.
	/// Optionally checks brain state via a delegate before sending acts.
	/// </summary>
	public class InputToActMapper : IDisposable
	{
		private IActor actor;
		private InputToActMapping mapping;
		private Func<string, bool> stateChecker;
		private bool holding;
		private Action<IPerformer> callback;

		public InputToActMapper(IActor actor, InputToActMapping mapping, Func<string, bool> stateChecker = null)
		{
			this.actor = actor;
			this.mapping = mapping;
			this.stateChecker = stateChecker;
		}

		public void Send(bool input, Action<IPerformer> callback = null)
		{
			if (input)
			{
				Hold(callback);
			}
			else
			{
				Release(callback);
			}
		}

		public void Hold(Action<IPerformer> callback = null)
		{
			if (!holding && IsStateValid())
			{
				this.callback = callback;
				holding = true;
				actor.Send(NewAct(true, callback));
			}
		}

		public void Release(Action<IPerformer> callback = null)
		{
			if (holding)
			{
				this.callback = callback;
				holding = false;
				actor.Send(NewAct(false, callback));
			}
		}

		public void Update()
		{
			if (mapping.HoldEveryFrame && holding)
			{
				actor.Send(NewAct(true, callback));
			}
		}

		public virtual void Dispose()
		{
			Release();
		}

		private Act<bool> NewAct(bool value, Action<IPerformer> callback = null)
		{
			return new Act<bool>(mapping, value, callback);
		}

		/// <summary>
		/// Checks whether the mapping's required state is currently active.
		/// Returns true if no state is required or no state checker is available.
		/// </summary>
		private bool IsStateValid()
		{
			if (string.IsNullOrEmpty(mapping.State))
			{
				return true;
			}
			if (stateChecker == null)
			{
				return true;
			}
			return stateChecker(mapping.State);
		}
	}
}
