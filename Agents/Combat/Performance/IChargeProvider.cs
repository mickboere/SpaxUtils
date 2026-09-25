namespace SpaxUtils
{
	/// <summary>
	/// A performance <see cref="BehaviourAsset"/> that accumulates a charge multiplier during its performance
	/// (e.g. a charged melee swing). Lets the owning <see cref="IMovePerformer"/> surface the live charge to
	/// callers (such as AI) without coupling to the concrete behaviour.
	/// </summary>
	public interface IChargeProvider
	{
		/// <summary>The current charge multiplier; 1 = uncharged, rising while charging.</summary>
		float ChargeMultiplier { get; }

		/// <summary>How charged this is, 0..1 of what the agent's Static pool could ever fund.</summary>
		float ChargeFraction { get; }

		/// <summary>Whether the pool ran dry — the charge is over and the auto-release is counting down.</summary>
		bool ChargeDepleted { get; }

		/// <summary>How much of the whole Static pool this charge has spent, 0..1.</summary>
		float ChargeSpent { get; }

		/// <summary>0..1 through the wait between the pool running dry and the auto-release; 0 before.</summary>
		float AutoReleaseProgress { get; }
	}
}
