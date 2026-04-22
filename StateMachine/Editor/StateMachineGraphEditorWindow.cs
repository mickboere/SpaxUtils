using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Editor window that hosts a <see cref="StateMachineGraphView"/> for editing <see cref="GraphAsset"/> assets.
	/// Opens automatically when a <see cref="GraphAsset"/> is double-clicked in the Project window.
	/// </summary>
	public class StateMachineGraphEditorWindow : EditorWindow
	{
		private StateMachineGraphView graphView;
		private GraphAsset currentAsset;

		[OnOpenAsset]
		public static bool Open(int instanceId, int line)
		{
			Object obj = EditorUtility.InstanceIDToObject(instanceId);
			if (obj is GraphAsset asset)
			{
				Open(asset);
				return true;
			}

			return false;
		}

		public static void Open(GraphAsset asset)
		{
			StateMachineGraphEditorWindow window = GetWindow<StateMachineGraphEditorWindow>();
			window.titleContent = new GUIContent(asset.name);
			window.Load(asset);
			window.Show();
		}

		private void OnEnable()
		{
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		private void OnUndoRedo()
		{
			if (currentAsset != null && graphView != null)
			{
				graphView.Load(currentAsset);
			}
		}

		private void CreateGUI()
		{
			graphView = new StateMachineGraphView();
			graphView.StretchToParentSize();
			rootVisualElement.Add(graphView);

			if (currentAsset != null)
			{
				graphView.Load(currentAsset);
			}
		}

		private void Load(GraphAsset asset)
		{
			currentAsset = asset;
			titleContent = new GUIContent(asset.name);

			if (graphView != null)
			{
				graphView.Load(asset);
			}
		}

		private void OnSelectionChange()
		{
			if (Selection.activeObject is GraphAsset selected && selected != currentAsset)
			{
				Load(selected);
			}
		}
	}
}
