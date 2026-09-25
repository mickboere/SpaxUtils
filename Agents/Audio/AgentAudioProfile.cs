using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(AgentAudioProfile), menuName = "Audio/" + nameof(AgentAudioProfile))]
	public class AgentAudioProfile : ScriptableObject
	{
		public TieredSFX Exertion => exertionTiers;
		public TieredSFX Damage => damageTiers;
		public SFXData Strain => strainSFX;

		[SerializeField] private TieredSFX exertionTiers = new TieredSFX();
		[SerializeField] private TieredSFX damageTiers = new TieredSFX();
		[SerializeField, Tooltip("Sustained effort loop while charging; volume and pitch ranges follow the charge. No grunt plays over it.")]
		private SFXData strainSFX;
		[SerializeField] private SFXData deathSFX;
		[SerializeField] private SFXData satisfySFX;
		[SerializeField] private ActionSFXData[] actionSFX;

		public SFXData GetDeathSFX() => deathSFX;
		public SFXData GetSatisfySFX() => satisfySFX;

		public SFXData GetActionSFX(string act)
		{
			if (string.IsNullOrWhiteSpace(act) || actionSFX == null || actionSFX.Length == 0)
			{
				return null;
			}

			for (int i = 0; i < actionSFX.Length; i++)
			{
				ActionSFXData entry = actionSFX[i];
				if (entry != null && entry.Act == act)
				{
					return entry.SFX;
				}
			}

			return null;
		}
	}
}
