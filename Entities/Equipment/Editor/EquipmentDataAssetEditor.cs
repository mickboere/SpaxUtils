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
		private StatMapping strengthMapping;
		private float baseStrength;

		protected override void OnEnable()
		{
			base.OnEnable();
			physicsDistributionProp = serializedObject.FindProperty("physicsDistribution");
			massProp = serializedObject.FindProperty("mass");
			strengthMapping = null;
		}

		protected override void AfterDrawProperty(SerializedProperty prop)
		{
			base.AfterDrawProperty(prop);

			EquipmentDataAsset eq = (EquipmentDataAsset)target;

			if (massProp != null && prop.propertyPath == massProp.propertyPath)
			{
				float effectiveMass = SpaxFormulas.EquipmentMass(eq.Mass, eq.Rank, eq.PhysicsDistribution,
					eq.Coverage, eq.SlotType == EquipmentSlotTypes.APPAREL);
				using (new EditorGUI.IndentLevelScope(1))
				{
					EditorGUILayout.LabelField("Effective Mass", effectiveMass.ToString("F2"));
					if (eq.SlotType != EquipmentSlotTypes.APPAREL)
					{
						EditorGUILayout.LabelField("Required Tenacity", RequiredTenacity(effectiveMass));
					}
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

			Vector8 physics = SpaxFormulas.EquipmentPhysics(eq.PhysicsDistribution, eq.Rank, eq.Quality, eq.Coverage);

			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField("Physics (preview)", physics.ToStringShort());
			}
		}

		/// <summary>Tenacity level whose Strength covers <paramref name="weaponMass"/>, read from the stat assets.</summary>
		private string RequiredTenacity(float weaponMass)
		{
			if (strengthMapping == null)
			{
				FindStrengthSources();
			}
			if (strengthMapping == null)
			{
				return "(no Tenacity→Strength mapping found)";
			}

			float needed = weaponMass - baseStrength;
			float tenacity = needed <= 0f ? 0f : strengthMapping.GetInverseModifierValue(needed);
			return Mathf.CeilToInt(Mathf.Max(0f, tenacity)).ToString();
		}

		private void FindStrengthSources()
		{
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(StatMap)))
			{
				StatMap map = AssetDatabase.LoadAssetAtPath<StatMap>(AssetDatabase.GUIDToAssetPath(guid));
				if (map != null && map.TryGetMapping(AgentStatIdentifiers.TENACITY_LVL,
					AgentStatIdentifiers.STRENGTH, out StatMapping mapping))
				{
					strengthMapping = mapping;
					break;
				}
			}

			baseStrength = 0f;
			foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(StatConfigurationSheet)))
			{
				StatConfigurationSheet sheet = AssetDatabase.LoadAssetAtPath<StatConfigurationSheet>(
					AssetDatabase.GUIDToAssetPath(guid));
				if (sheet != null && sheet.TryGet(AgentStatIdentifiers.STRENGTH, out IStatConfiguration config))
				{
					baseStrength = config.DefaultValue;
					break;
				}
			}
		}
	}
}
#endif
