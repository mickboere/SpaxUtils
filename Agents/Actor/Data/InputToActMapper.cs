using System;

namespace SpaxUtils
{
	/// <summary>
	/// Instanced helper class that uses an <see cref="InputToActMapping"/> to map boolean input to actor acts.
	/// </summary>
	public class InputToActMapper : IDisposable
	{
		private IActor actor;
		private InputToActMapping mapping;

		private bool holding;
		private Action<IPerformer> callback;

		public InputToActMapper(IActor actor, InputToActMapping mapping)
		{
			this.actor = actor;
			this.mapping = mapping;
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
			if (!holding)
			{
				this.callback = callback;
				actor.Send(NewAct(true, callback));
				holding = true;
			}
		}

		public void Release(Action<IPerformer> callback = null)
		{
			if (holding)
			{
				this.callback = callback;
				actor.Send(NewAct(false, callback));
				holding = false;
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
	}
}
