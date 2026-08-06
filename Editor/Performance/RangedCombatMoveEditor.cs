using UnityEditor;

namespace SpaxUtils
{
	/// <summary>
	/// Adds the ranged group to the <see cref="PerformanceMoveEditor"/> lens. InstanceDelay stays a field:
	/// no marker identifier describes a projectile spawn yet, and none is added until something reads it.
	/// </summary>
	[CustomEditor(typeof(RangedCombatMove), true)]
	public class RangedCombatMoveEditor : PerformanceMoveEditor
	{
		protected override void BuildAdditionalGroups()
		{
			Group("Ranged")
				.Field("projectilePrefab")
				.Field("instanceLocation")
				.Field("instanceDelay");
		}
	}
}
