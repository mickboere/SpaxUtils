// EquipmentDataAssetEditor.cs (updated preview to match ratio-correct allocation)
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomEditor(typeof(EquipmentDataAsset), true)]
	public class EquipmentDataAssetEditor : ItemDataAssetEditor
	{
		private SerializedProperty physicsDistributionProp;
		private SerializedProperty massProp;

		protected override void OnEnable()
		{
			base.OnEnable();
			physicsDistributionProp = serializedObject.FindProperty("physicsDistribution");
			massProp = serializedObject.FindProperty("mass");
		}

		protected override void AfterDrawProperty(SerializedProperty prop)
		{
			base.AfterDrawProperty(prop);

			EquipmentDataAsset eq = (EquipmentDataAsset)target;

			if (massProp != null && prop.propertyPath == massProp.propertyPath)
			{
				float dist = eq.PhysicsDistribution[0].Max(eq.PhysicsDistribution[6]);
				float effectiveMass = eq.Mass + ItemDataAsset.POWER_MASS_FACTOR * eq.Mass * eq.Rank * dist;
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.LabelField("Effective Mass", effectiveMass.ToString("F2"));
				}
			}

			if (physicsDistributionProp == null) return;
			if (prop.propertyPath != physicsDistributionProp.propertyPath) return;

			float budget = SpaxFormulas.PointsFromRank(eq.Rank) * eq.Quality;

			Vector8 lanePoints = SpaxFormulas.AllocatePointsForLevelRatios(eq.PhysicsDistribution, budget);

			Vector8 physics = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float lvl = lanePoints[i] <= 0f ? 0f : SpaxFormulas.LevelFromPoints(lanePoints[i]);
				physics[i] = Mathf.Round(lvl * 6f * eq.PhysicsScaling);
			}

			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField("Physics (preview)", physics.ToStringShort());
			}
		}
	}
}
#endif
