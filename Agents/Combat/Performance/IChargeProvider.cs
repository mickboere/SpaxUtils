namespace SpaxUtils
{
	/// <summary>
	/// A performance <see cref="BehaviourAsset"/> that accumulates a charge multiplier during its performance
	/// (e.g. a charged melee swing). Lets the owning <see cref="IMovePerformer"/> surface the live charge to
	/// callers (such as AI) without coupling to the concrete behaviour.
	/// </summary>
	public interface IChargeProvider
	{
		/// <summary>The current charge multiplier; 1 = uncharged, rising while charging (capped at the move's max).</summary>
		float ChargeMultiplier { get; }
	}
}
