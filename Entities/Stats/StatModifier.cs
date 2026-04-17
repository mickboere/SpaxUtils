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
		private bool sourceBase;

		public StatModifier(IStatModConfig config, CompositeFloatBase modifierStat, bool sourceBase)
		{
			this.Config = config;
			this.modifierStat = modifierStat;
			this.sourceBase = sourceBase;

			modifierStat.CompositeChangedEvent += OnInputStatChanged;
		}

		public override void Dispose()
		{
			if (modifierStat != null)
			{
				modifierStat.CompositeChangedEvent -= OnInputStatChanged;
			}
			base.Dispose();
		}

		public override float Modify(float input)
		{
			return FloatOperationModifier.Operate(input, Config.Operation, Config.GetModifierValue(sourceBase ? modifierStat.ModdedBaseValue : modifierStat.Value));
		}

		private void OnInputStatChanged(CompositeFloatBase composite)
		{
			Recalculate();
		}
	}
}
