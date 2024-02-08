using System;

namespace SpaxUtils
{
	/// <summary>
	/// Float modifier that uses another stat as mod value.
	/// </summary>
	public class StatModifier : FloatModifierBase, IDisposable
	{
		public override ModMethod Method => Config.Method;
		public IStatModConfig Config { get; private set; }

		private CompositeFloatBase modifierStat;

		public StatModifier(IStatModConfig config, CompositeFloatBase modifierStat)
		{
			this.Config = config;
			this.modifierStat = modifierStat;

			modifierStat.CompositeChangedEvent += OnInputStatChanged;
		}

		public void Dispose()
		{
			if (modifierStat != null)
			{
				modifierStat.CompositeChangedEvent -= OnInputStatChanged;
			}
		}

		public override float Modify(float input)
		{
			return FloatOperationModifier.Operate(input, Config.Operation, Config.GetModifierValue(modifierStat.Value));
		}

		private void OnInputStatChanged(CompositeFloatBase composite)
		{
			Dirty = true;
		}
	}
}
