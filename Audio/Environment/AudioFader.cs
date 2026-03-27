using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpaxUtils
{
	public class AudioFader : IDisposable
	{
		private static readonly object BaseLayerKey = new object();

		private sealed class LayerState : IDisposable
		{
			public object Key;
			public AudioSource Source;

			public AudioClip Clip;
			public bool Loop;
			public float StartTime;
			public float StartDelayRemaining;
			public bool StartedPlayback;

			public TransitionHelper IntrinsicTransition;
			public TransitionHelper MaskTransition;
			public TransitionSettings IntrinsicSettings;
			public TransitionSettings MaskSettings;

			public bool RemoveWhenMaskSilent;

			public bool IsBaseLayer => ReferenceEquals(Key, BaseLayerKey);

			public float Volume
			{
				get
				{
					float intrinsic = IntrinsicTransition != null ? IntrinsicTransition.Evaluation : 1f;
					float mask = MaskTransition != null ? MaskTransition.Evaluation : 1f;
					return intrinsic * mask;
				}
			}

			public void Dispose()
			{
				IntrinsicTransition?.Dispose();
				IntrinsicTransition = null;

				MaskTransition?.Dispose();
				MaskTransition = null;

				if (Source)
				{
					GameObject sourceObject = Source.gameObject;
					if (sourceObject)
					{
#if UNITY_EDITOR
						if (!Application.isPlaying)
						{
							Object.DestroyImmediate(sourceObject);
						}
						else
						{
							Object.Destroy(sourceObject);
						}
#else
						Object.Destroy(sourceObject);
#endif
					}
				}

				Source = null;
			}
		}

		private readonly AudioSource sourceTemplate;
		private readonly CallbackService callbackService;
		private readonly List<LayerState> layers = new List<LayerState>(4);

		private bool isDisposed;

		public AudioFader(AudioSource sourceTemplate, CallbackService callbackService)
		{
			this.sourceTemplate = sourceTemplate;
			this.callbackService = callbackService;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public void Dispose()
		{
			if (isDisposed)
			{
				return;
			}

			isDisposed = true;

			callbackService.UnsubscribeUpdates(this);

			for (int i = layers.Count - 1; i >= 0; i--)
			{
				layers[i].Dispose();
			}

			layers.Clear();

			if (sourceTemplate)
			{
				GameObject templateObject = sourceTemplate.gameObject;
				if (templateObject)
				{
#if UNITY_EDITOR
					if (!Application.isPlaying)
					{
						Object.DestroyImmediate(templateObject);
					}
					else
					{
						Object.Destroy(templateObject);
					}
#else
					Object.Destroy(templateObject);
#endif
				}
			}
		}

		public void SetBase(AudioClip clip, TransitionSettings settings, float clipDelay = 0f, bool loop = true, float startTime = 0f, bool hidden = false)
		{
			if (isDisposed)
			{
				return;
			}

			LayerState existingBase = FindBaseLayer();

			if (existingBase != null && BaseMatches(existingBase, clip, loop, startTime, clipDelay))
			{
				existingBase.IntrinsicSettings = settings;
				existingBase.MaskSettings = settings;

				SetMaskImmediate(existingBase, hidden ? 0f : 1f);
				return;
			}

			if (hidden)
			{
				// While hidden under an override, just replace the base state silently.
				// No audible transition is desired here because the override owns audibility.
				if (existingBase != null)
				{
					RemoveLayerImmediate(existingBase);
				}

				LayerState hiddenBase = CreateLayer(
					BaseLayerKey,
					clip,
					settings,
					clipDelay,
					loop,
					startTime,
					initialMaskProgress: 0f,
					intrinsicDelay: clipDelay);

				if (hiddenBase != null)
				{
					layers.Insert(0, hiddenBase);
				}

				return;
			}

			float newBaseDelay = clipDelay;

			if (existingBase != null)
			{
				TransitionSettings oldBaseSettings = existingBase.MaskSettings ?? existingBase.IntrinsicSettings;

				DemoteLayer(existingBase);
				FadeMaskOut(existingBase, oldBaseSettings);
				existingBase.RemoveWhenMaskSilent = true;

				newBaseDelay += GetBaseOverlapDelay(oldBaseSettings, settings);
			}

			LayerState newBase = CreateLayer(
				BaseLayerKey,
				clip,
				settings,
				clipDelay,
				loop,
				startTime,
				initialMaskProgress: 1f,
				intrinsicDelay: newBaseDelay);

			if (newBase != null)
			{
				layers.Add(newBase);
			}
		}

		/// <summary>
		/// Fades out and removes the current base layer without creating a replacement.
		/// </summary>
		public void ClearBase()
		{
			if (isDisposed)
			{
				return;
			}

			LayerState baseLayer = FindBaseLayer();
			if (baseLayer == null)
			{
				return;
			}

			TransitionSettings settings = baseLayer.MaskSettings ?? baseLayer.IntrinsicSettings;
			FadeMaskOut(baseLayer, settings);
			baseLayer.RemoveWhenMaskSilent = true;
		}

		public void PushOverride(object key, AudioClip clip, TransitionSettings settings, float clipDelay = 0f, bool loop = true, float startTime = 0f)
		{
			if (isDisposed)
			{
				return;
			}

			if (key == null)
			{
				Debug.LogError($"{nameof(AudioFader)}.{nameof(PushOverride)} called with null key.");
				return;
			}

			LayerState existing = FindLayer(key);
			if (existing != null)
			{
				LayerState currentTopExisting = GetTopLayer();

				if (ReferenceEquals(existing, currentTopExisting) && LayerContentMatches(existing, clip, loop))
				{
					existing.RemoveWhenMaskSilent = false;
					existing.MaskSettings = settings;

					ResumeMaskIn(existing, settings);

					LayerState belowExisting = GetLayerBelow(existing);
					if (belowExisting != null)
					{
						// Override settings own the outgoing fade.
						FadeMaskOut(belowExisting, settings);
					}

					return;
				}

				RemoveLayerImmediate(existing);
			}

			LayerState currentTop = GetTopLayer();
			if (currentTop != null)
			{
				// Override settings own the outgoing fade.
				FadeMaskOut(currentTop, settings);
			}

			LayerState overrideLayer = CreateLayer(
				key,
				clip,
				settings,
				clipDelay,
				loop,
				startTime,
				initialMaskProgress: 1f,
				intrinsicDelay: clipDelay + (currentTop != null ? GetOverrideOverlapDelay(settings) : 0f));

			if (overrideLayer != null)
			{
				layers.Add(overrideLayer);
			}
		}

		public bool PopOverride(object key, TransitionSettings settings)
		{
			if (isDisposed)
			{
				return false;
			}

			if (key == null)
			{
				Debug.LogError($"{nameof(AudioFader)}.{nameof(PopOverride)} called with null key.");
				return false;
			}

			LayerState target = FindLayer(key);
			if (target == null)
			{
				return false;
			}

			LayerState top = GetTopLayer();
			if (!ReferenceEquals(target, top))
			{
				return false;
			}

			if (target.RemoveWhenMaskSilent)
			{
				return true;
			}

			LayerState below = GetLayerBelow(target);

			// Override settings own both sides of the pop transition too.
			FadeMaskOut(target, settings);
			target.RemoveWhenMaskSilent = true;

			if (below != null)
			{
				FadeMaskIn(below, settings);
			}

			return true;
		}

		public void ClearOverrides()
		{
			if (isDisposed)
			{
				return;
			}

			for (int i = layers.Count - 1; i >= 0; i--)
			{
				LayerState layer = layers[i];
				if (!layer.IsBaseLayer)
				{
					RemoveLayerImmediate(layer);
				}
			}

			LayerState baseLayer = FindBaseLayer();
			if (baseLayer != null)
			{
				SetMaskImmediate(baseLayer, 1f);
			}
		}

		private void OnUpdate(float delta)
		{
			if (isDisposed)
			{
				return;
			}

			for (int i = 0; i < layers.Count; i++)
			{
				LayerState layer = layers[i];

				UpdateStartDelay(layer, delta);
				UpdateTransition(layer.IntrinsicTransition);
				UpdateTransition(layer.MaskTransition);

				if (layer.Source)
				{
					layer.Source.volume = layer.Volume;
				}
			}

			CleanupSilentLayers();
		}

		private void UpdateStartDelay(LayerState layer, float delta)
		{
			if (layer == null || layer.Clip == null || layer.StartedPlayback)
			{
				return;
			}

			layer.StartDelayRemaining -= delta;
			if (layer.StartDelayRemaining <= 0f)
			{
				StartPlayback(layer);
			}
		}

		private void UpdateTransition(TransitionHelper transition)
		{
			transition?.TryUpdateProgress();
		}

		private void StartPlayback(LayerState layer)
		{
			if (isDisposed || layer == null || layer.Source == null || layer.Clip == null)
			{
				return;
			}

			float length = layer.Clip.length;
			layer.Source.time = length > 0f ? Mathf.Repeat(layer.StartTime, length) : 0f;
			layer.Source.Play();
			layer.StartedPlayback = true;
			layer.StartDelayRemaining = 0f;
		}

		private LayerState CreateLayer(
			object key,
			AudioClip clip,
			TransitionSettings settings,
			float clipDelay,
			bool loop,
			float startTime,
			float initialMaskProgress,
			float intrinsicDelay)
		{
			if (isDisposed || sourceTemplate == null)
			{
				return null;
			}

			AudioSource source = Object.Instantiate(sourceTemplate);
			if (source == null)
			{
				return null;
			}

			Object.DontDestroyOnLoad(source.gameObject);

			source.Stop();
			source.volume = 0f;
			source.clip = clip;
			source.loop = loop;

			LayerState layer = new LayerState
			{
				Key = key,
				Source = source,
				Clip = clip,
				Loop = loop,
				StartTime = startTime,
				StartDelayRemaining = Mathf.Max(0f, clipDelay),
				StartedPlayback = false,
				IntrinsicSettings = settings,
				MaskSettings = settings,
				IntrinsicTransition = BuildTransition(settings, 0f),
				MaskTransition = BuildTransition(settings, initialMaskProgress),
				RemoveWhenMaskSilent = false
			};

			layer.IntrinsicTransition.Fill(delay: Mathf.Max(0f, intrinsicDelay));

			if (clip != null && layer.StartDelayRemaining <= 0f)
			{
				StartPlayback(layer);
			}

			return layer;
		}

		private void FadeMaskOut(LayerState layer, TransitionSettings settings)
		{
			if (layer == null)
			{
				return;
			}

			float progress = layer.MaskTransition != null ? layer.MaskTransition.Progress : 1f;
			layer.MaskTransition?.Dispose();
			layer.MaskTransition = BuildTransition(settings, progress);
			layer.MaskSettings = settings;
			layer.MaskTransition.Empty();
		}

		private void FadeMaskIn(LayerState layer, TransitionSettings settings)
		{
			if (layer == null)
			{
				return;
			}

			float progress = layer.MaskTransition != null ? layer.MaskTransition.Progress : 0f;
			layer.MaskTransition?.Dispose();
			layer.MaskTransition = BuildTransition(settings, progress);
			layer.MaskSettings = settings;
			layer.MaskTransition.Fill(delay: GetOverrideOverlapDelay(settings));
		}

		private void ResumeMaskIn(LayerState layer, TransitionSettings settings)
		{
			if (layer == null)
			{
				return;
			}

			float progress = layer.MaskTransition != null ? layer.MaskTransition.Progress : 0f;
			layer.MaskTransition?.Dispose();
			layer.MaskTransition = BuildTransition(settings, progress);
			layer.MaskSettings = settings;
			layer.MaskTransition.Fill();
		}

		private void SetMaskImmediate(LayerState layer, float progress)
		{
			if (layer == null)
			{
				return;
			}

			if (layer.MaskTransition == null)
			{
				layer.MaskTransition = BuildTransition(layer.MaskSettings, progress);
				return;
			}

			if (progress >= 1f)
			{
				layer.MaskTransition.FillImmediately();
			}
			else
			{
				layer.MaskTransition.EmptyImmediately();
			}
		}

		private void CleanupSilentLayers()
		{
			for (int i = layers.Count - 1; i >= 0; i--)
			{
				LayerState layer = layers[i];
				if (!layer.RemoveWhenMaskSilent)
				{
					continue;
				}

				if (layer.MaskTransition == null || layer.MaskTransition.IsEmpty)
				{
					RemoveLayerImmediate(layer);
				}
			}
		}

		private void RemoveLayerImmediate(LayerState layer)
		{
			if (layer == null)
			{
				return;
			}

			layers.Remove(layer);
			layer.Dispose();
		}

		private void DemoteLayer(LayerState layer)
		{
			if (layer == null)
			{
				return;
			}

			layer.Key = new object();
		}

		private LayerState GetTopLayer()
		{
			return layers.Count > 0 ? layers[layers.Count - 1] : null;
		}

		private LayerState GetLayerBelow(LayerState layer)
		{
			int index = layers.IndexOf(layer);
			return index > 0 ? layers[index - 1] : null;
		}

		private LayerState FindLayer(object key)
		{
			for (int i = 0; i < layers.Count; i++)
			{
				if (ReferenceEquals(layers[i].Key, key))
				{
					return layers[i];
				}
			}

			return null;
		}

		private LayerState FindBaseLayer()
		{
			return FindLayer(BaseLayerKey);
		}

		private bool LayerContentMatches(LayerState layer, AudioClip clip, bool loop)
		{
			return layer != null &&
				layer.Clip == clip &&
				layer.Loop == loop;
		}

		private bool BaseMatches(LayerState layer, AudioClip clip, bool loop, float startTime, float clipDelay)
		{
			if (layer == null || layer.Clip != clip || layer.Loop != loop)
			{
				return false;
			}

			if (layer.StartedPlayback)
			{
				return true;
			}

			return Mathf.Approximately(layer.StartTime, startTime) &&
				Mathf.Approximately(layer.StartDelayRemaining, Mathf.Max(0f, clipDelay));
		}

		private TransitionHelper BuildTransition(TransitionSettings settings, float initialProgress)
		{
			TransitionHelper transition = settings != null
				? new TransitionHelper(
					settings.Realtime,
					1f,
					settings.InTime,
					settings.OutTime,
					settings.Intro,
					settings.Outro)
				: new TransitionHelper();

			transition.Progress = Mathf.Clamp01(initialProgress);
			return transition;
		}

		private float GetBaseOverlapDelay(TransitionSettings oldBaseSettings, TransitionSettings newBaseSettings)
		{
			float oldOutTime = oldBaseSettings != null ? Mathf.Max(0f, oldBaseSettings.OutTime) : 0f;
			float relativeDelay = newBaseSettings != null ? Mathf.Clamp01(newBaseSettings.RelativeDelay) : 0f;
			return oldOutTime * relativeDelay;
		}

		private float GetOverrideOverlapDelay(TransitionSettings overrideSettings)
		{
			if (overrideSettings == null)
			{
				return 0f;
			}

			return Mathf.Max(0f, overrideSettings.OutTime) * Mathf.Clamp01(overrideSettings.RelativeDelay);
		}
	}
}
