using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(AgentAudioProfile), menuName = "Audio/" + nameof(AgentAudioProfile))]
	public class AgentAudioProfile : ScriptableObject
	{
		[SerializeField] private ImpactSFXData[] exertionSFX;
		[SerializeField] private ImpactSFXData[] damageSFX;
		[SerializeField] private SFXData deathSFX;
		[SerializeField] private SFXData satisfySFX;
		[SerializeField] private ActionSFXData[] actionSFX;

		public SFXData GetExertionSFX(float intensity) => GetIntensitySFX(exertionSFX, intensity);
		public SFXData GetDamageSFX(float intensity) => GetIntensitySFX(damageSFX, intensity);
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

		private SFXData GetIntensitySFX(ImpactSFXData[] entries, float intensity)
		{
			if (entries == null || entries.Length == 0)
			{
				return null;
			}
			else if (entries.Length == 1)
			{
				return entries[0].SFX;
			}

			intensity = Mathf.Clamp01(intensity);

			ImpactSFXData match = null;
			ImpactSFXData fallback = null;

			for (int i = 0; i < entries.Length; i++)
			{
				ImpactSFXData entry = entries[i];
				if (entry == null || entry.SFX == null)
				{
					continue;
				}

				if (fallback == null || entry.Intensity < fallback.Intensity)
				{
					fallback = entry;
				}

				if (entry.Intensity <= intensity && (match == null || entry.Intensity > match.Intensity))
				{
					match = entry;
				}
			}

			return match != null ? match.SFX : fallback != null ? fallback.SFX : null;
		}
	}
}
