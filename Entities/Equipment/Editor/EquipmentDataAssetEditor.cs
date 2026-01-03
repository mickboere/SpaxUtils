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

		protected override void OnEnable()
		{
			base.OnEnable();
			physicsDistributionProp = serializedObject.FindProperty("physicsDistribution");
		}

		protected override void AfterDrawProperty(SerializedProperty prop)
		{
			base.AfterDrawProperty(prop);

			if (physicsDistributionProp == null) return;
			if (prop.propertyPath != physicsDistributionProp.propertyPath) return;

			EquipmentDataAsset eq = (EquipmentDataAsset)target;

			float budget = SpaxFormulas.PointsFromRank(eq.Rank) * eq.Quality;

			Vector8 lanePoints = SpaxFormulas.AllocatePointsForLevelRatios(eq.PhysicsDistribution, budget);

			Vector8 physics = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float lvl = lanePoints[i] <= 0f ? 0f : SpaxFormulas.LevelFromPoints(lanePoints[i]);
				physics[i] = Mathf.Round(lvl * 4f);
			}

			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField("Physics (preview)", physics.ToStringShort());
			}
		}
	}
}
#endif
