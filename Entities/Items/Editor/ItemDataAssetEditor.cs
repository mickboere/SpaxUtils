#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomEditor(typeof(ItemDataAsset), true)]
	public class ItemDataAssetEditor : Editor
	{
		private SerializedProperty rankProp;
		private SerializedProperty qualityProp;
		private SerializedProperty valueProp;
		private SerializedProperty rarityProp;

		protected virtual void OnEnable()
		{
			rankProp = serializedObject.FindProperty("rank");
			qualityProp = serializedObject.FindProperty("quality");
			valueProp = serializedObject.FindProperty("value");
			rarityProp = serializedObject.FindProperty("rarity");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			SerializedProperty it = serializedObject.GetIterator();
			bool enterChildren = true;

			while (it.NextVisible(enterChildren))
			{
				enterChildren = false;

				if (it.name == "m_Script")
				{
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.PropertyField(it, true);
					}
					continue;
				}

				// Skip "rarity" where it normally appears; we re-draw it under Budget (after Value).
				if (rarityProp != null && it.propertyPath == rarityProp.propertyPath)
				{
					continue;
				}

				EditorGUILayout.PropertyField(it, true);
				AfterDrawProperty(it);
			}

			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void AfterDrawProperty(SerializedProperty prop)
		{
			if (valueProp == null) return;
			if (prop.propertyPath != valueProp.propertyPath) return;

			float rank = rankProp != null ? rankProp.floatValue : 0f;
			float quality = qualityProp != null ? qualityProp.floatValue : 0f;

			float budget = SpaxFormulas.PointsFromRank(rank) * quality;
			int budgetRounded = Mathf.RoundToInt(budget);

			using (new EditorGUI.IndentLevelScope(1))
			{
				EditorGUILayout.LabelField("Budget", budgetRounded.ToString());

				// Re-draw Rarity right here (below Budget, above Icon).
				if (rarityProp != null)
				{
					EditorGUILayout.PropertyField(rarityProp, true);

					ItemRarity auto = SpaxFormulas.GetRarityFromQuality(quality);
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.EnumPopup("Auto Rarity", auto);
					}
				}
			}
		}
	}
}
#endif
