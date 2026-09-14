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
				float effectiveMass = SpaxFormulas.EquipmentMass(eq.Mass, eq.Rank, eq.PhysicsDistribution);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.LabelField("Effective Mass", effectiveMass.ToString("F2"));
				}
			}

			if (physicsDistributionProp == null)
			{
				return;
			}
			if (prop.propertyPath != physicsDistributionProp.propertyPath)
			{
				return;
			}

			Vector8 physics = SpaxFormulas.EquipmentPhysics(eq.PhysicsDistribution, eq.Rank, eq.Quality, eq.PhysicsScaling);

			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField("Physics (preview)", physics.ToStringShort());
			}
		}
	}
}
#endif
