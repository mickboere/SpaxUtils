using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Editor utilities for the GraphAsset system.
	/// xNode connection recovery has been removed — base classes no longer extend XNode.Node,
	/// so port data cannot be read via the xNode API. Use the graph editor to reconnect nodes manually.
	/// </summary>
	public static class XNodeToGraphConverter
	{
		[MenuItem("Tools/StateMachine/Repair Missing Node GUIDs")]
		public static void RepairMissingGuids()
		{
			string[] assetGuids = AssetDatabase.FindAssets("t:GraphAsset");
			int repaired = 0;

			foreach (string assetGuid in assetGuids)
			{
				string path = AssetDatabase.GUIDToAssetPath(assetGuid);
				GraphAsset asset = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
				if (asset == null)
				{
					continue;
				}

				bool dirty = false;
				foreach (GraphNodeBase node in asset.Nodes)
				{
					if (node == null || !string.IsNullOrEmpty(node.Guid))
					{
						continue;
					}

					FieldInfo guidField = typeof(GraphNodeBase).GetField("guid", BindingFlags.Instance | BindingFlags.NonPublic);
					if (guidField != null)
					{
						guidField.SetValue(node, Guid.NewGuid().ToString());
						EditorUtility.SetDirty(node);
						dirty = true;
						repaired++;
					}
				}

				if (dirty)
				{
					EditorUtility.SetDirty(asset);
				}
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[XNodeToGraphConverter] Repaired GUIDs on {repaired} node(s).");
		}
	}
}
