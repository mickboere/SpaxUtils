using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace SpaxUtils
{
	/// <summary>
	/// Writes runtime values into shared assets and restores the originals on exit, so settings never dirty the project.
	/// Needed because the editor keeps these writes across play sessions.
	/// </summary>
	public static class RuntimeAssetGuard
	{
		private static readonly Dictionary<(Object, string), Action> restores = new Dictionary<(Object, string), Action>();

		public static void SetFloat(Material material, string property, float value)
		{
			if (material == null)
			{
				return;
			}
			float original = material.GetFloat(property);
			Record(material, property, () => material.SetFloat(property, original));
			material.SetFloat(property, value);
		}

		public static void SetActive(ScriptableRendererFeature feature, bool active)
		{
			if (feature == null)
			{
				return;
			}
			bool original = feature.isActive;
			Record(feature, nameof(feature.isActive), () => feature.SetActive(original));
			feature.SetActive(active);
		}

		public static void SetRenderScale(UniversalRenderPipelineAsset asset, float scale)
		{
			if (asset == null)
			{
				return;
			}
			float original = asset.renderScale;
			Record(asset, nameof(asset.renderScale), () => asset.renderScale = original);
			asset.renderScale = scale;
		}

		/// <summary>
		/// Quality settings persist into the project when changed in the editor.
		/// </summary>
		public static void SetVSyncCount(int count)
		{
			int level = QualitySettings.GetQualityLevel();
			int original = QualitySettings.vSyncCount;
			Record(null, $"{nameof(QualitySettings.vSyncCount)}{level}", () =>
			{
				if (QualitySettings.GetQualityLevel() == level)
				{
					QualitySettings.vSyncCount = original;
				}
			});
			QualitySettings.vSyncCount = count;
		}

		/// <summary>
		/// Puts every guarded asset back to the value it had before the first write.
		/// </summary>
		public static void RestoreAll()
		{
			foreach (KeyValuePair<(Object, string), Action> restore in restores)
			{
				// Skip destroyed assets; a null key is a non-asset target (quality settings).
				if (restore.Key.Item1 is Object asset && asset == null)
				{
					continue;
				}
				restore.Value();
			}
			restores.Clear();
		}

		private static void Record(Object asset, string key, Action restore)
		{
			// Only the first write knows the true original.
			if (restores.ContainsKey((asset, key)))
			{
				return;
			}
			restores.Add((asset, key), restore);

			Application.quitting -= RestoreAll;
			Application.quitting += RestoreAll;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
		}

#if UNITY_EDITOR
		private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
		{
			if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
			{
				RestoreAll();
			}
		}
#endif
	}
}
