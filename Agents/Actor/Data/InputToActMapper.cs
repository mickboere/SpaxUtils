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

		public InputToActMapper(IActor actor, InputToActMapping mapping)
		{
			this.actor = actor;
			this.mapping = mapping;
		}

		public void Send(bool input)
		{
			if (input)
			{
				Hold();
			}
			else
			{
				Release();
			}
		}

		public void Hold()
		{
			if (!holding)
			{
				actor.Send(NewAct(true));
				holding = true;
			}
		}

		public void Release()
		{
			if (holding)
			{
				actor.Send(NewAct(false));
				holding = false;
			}
		}

		public void Update()
		{
			if (mapping.HoldEveryFrame && holding)
			{
				actor.Send(NewAct(true));
			}
		}

		public virtual void Dispose()
		{
			Release();
		}

		private Act<bool> NewAct(bool value)
		{
			return new Act<bool>(mapping, value);
		}
	}
}
