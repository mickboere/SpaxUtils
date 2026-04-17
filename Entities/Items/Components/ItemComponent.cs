using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRuntimeItemDataComponent"/> implementation, containing a reference to an <see cref="IRuntimeItemData"/>.
	/// Component implements <see cref="IInteractable"/>, once sucessfully interacted with the Entity will be destroyed.
	/// </summary>
	public class ItemComponent : InteractableComponentBase, IRuntimeItemDataComponent
	{
		/// <inheritdoc/>
		public RuntimeItemData RuntimeItemData { get; private set; }

		public override string InteractableType => InteractionTypes.ITEM;

		[SerializeField] private bool autoUpdateID;
		[SerializeField] private SerializedItemData item;

#if UNITY_EDITOR
		[SerializeField, HideInInspector] private int lastItemDataHash;
#endif

		protected virtual void OnValidate()
		{
			UpdateIdentification();
		}

		protected virtual void Awake()
		{
			UpdateIdentification();
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public override bool TryInteract(IInteraction interaction)
		{
			interaction.Data = RuntimeItemData;
			gameObject.SetActive(false);
			Entity.RuntimeData.SetValue(EntityDataIdentifiers.OFF, true);
			return true;
		}

		private void UpdateIdentification()
		{
			if (item == null || item.Asset == null || Entity == null)
			{
				return;
			}

			// Always keep label.
			Entity.Identification.Add(EntityLabels.ITEM);

#if UNITY_EDITOR
			// Only allow editor-side identification sync when not playing.
			if (!Application.isPlaying)
			{
				Entity entityComponent = GetEntityComponent();
				if (entityComponent == null)
				{
					return;
				}

				bool changed = false;

				// Keep the stored hash in sync so re-enabling auto update does not immediately reroll
				// from stale comparison data.
				int currentHash = GetEditorItemHash();

				if (lastItemDataHash != currentHash)
				{
					RecordEditorObjects(entityComponent);
					lastItemDataHash = currentHash;
					changed = true;

					if (autoUpdateID)
					{
						Entity.Identification.Name = item.Asset.Identification.Name;
						Entity.Identification.ID = Guid.NewGuid().ToString();
					}
				}
				else if (Entity.Identification.Name != item.Asset.Identification.Name)
				{
					// Name sync is also considered part of auto update behavior.
					if (autoUpdateID)
					{
						RecordEditorObjects(entityComponent);
						Entity.Identification.Name = item.Asset.Identification.Name;
						changed = true;
					}
				}

				if (changed)
				{
					PersistEditorObjects(entityComponent);
				}
			}
#else
			// In builds/play mode: never touch ID. Optionally sync name only.
			if (Entity.Identification.Name != item.Asset.Identification.Name)
			{
				Entity.Identification.Name = item.Asset.Identification.Name;
			}
#endif
		}

		private void RefreshRuntimeItemData()
		{
			RuntimeItemData = item.ToRuntimeItemData();
		}

#if UNITY_EDITOR
		private Entity GetEntityComponent()
		{
			return gameObject.GetComponentInParent<Entity>();
		}

		private int GetEditorItemHash()
		{
			string assetKey = GetEditorAssetKey();
			string dataJson = item.Data == null ? string.Empty : JsonUtility.ToJson(item.Data);
			return (assetKey + "|" + dataJson).GetDeterministicHashCode();
		}

		private string GetEditorAssetKey()
		{
			string path = AssetDatabase.GetAssetPath(item.Asset);
			if (!string.IsNullOrEmpty(path))
			{
				string guid = AssetDatabase.AssetPathToGUID(path);
				if (!string.IsNullOrEmpty(guid))
				{
					return guid;
				}
			}

			return item.Asset.ID ?? item.Asset.name;
		}

		private void RecordEditorObjects(Entity entityComponent)
		{
			Undo.RecordObject(this, "Update Item Identification");
			Undo.RecordObject(entityComponent, "Update Item Identification");
		}

		private void PersistEditorObjects(Entity entityComponent)
		{
			EditorUtility.SetDirty(this);
			EditorUtility.SetDirty(entityComponent);

			PrefabUtility.RecordPrefabInstancePropertyModifications(this);
			PrefabUtility.RecordPrefabInstancePropertyModifications(entityComponent);

			if (gameObject.scene.IsValid())
			{
				EditorSceneManager.MarkSceneDirty(gameObject.scene);
			}
		}
#endif
	}
}
