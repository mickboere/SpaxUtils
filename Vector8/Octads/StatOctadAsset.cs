using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(StatOctadAsset), menuName = "ScriptableObjects/Stats/" + nameof(StatOctadAsset))]
	public class StatOctadAsset : ScriptableObject
	{
		[SerializeField] private StatOctad statOctad;

		/// <summary>
		/// Generate and initialize a new stat octad for <paramref name="entity"/>.
		/// </summary>
		/// <param name="entity">The entity to generate the stat octad for.</param>
		/// <param name="defaultValues">The default values for missing stats.</param>
		public StatOctad Get(IEntity entity, Vector8 defaultValues = default)
		{
			return new StatOctad(entity, statOctad, defaultValues);
		}
	}
}
