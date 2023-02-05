using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//namespace SpaxUtils
//{
//	[CustomEditor(typeof(StatLibrary))]
//	public class StatLibraryEditor : Editor
//	{
//		public override void OnInspectorGUI()
//		{
//			if (GUILayout.Button("Generate Stats"))
//			{
//				Undo.RecordObject(target, "Generate Stats");
//				((StatLibrary)target).GenerateStats();
//			}
//			if (GUILayout.Button("RE-generate Stats"))
//			{
//				if (EditorUtility.DisplayDialog(
//					"Are you SURE?",
//					"This will delete all of your current settings and re-generate them.",
//					"Yes, throw everything away",
//					"Cancel"))
//				{
//					Undo.RecordObject(target, "Regenerate Stats");
//					((StatLibrary)target).RegenerateStats();
//				}
//			}
//			base.OnInspectorGUI();
//		}
//	}
//}
