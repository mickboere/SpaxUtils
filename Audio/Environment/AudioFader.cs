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
				MaskTransition?.Dispose();

				if (Source)
				{
					Object.Destroy(Source.gameObject);
				}
			}
		}

		private readonly AudioSource sourceTemplate;
		private readonly CallbackService callbackService;
		private readonly List<LayerState> layers = new List<LayerState>(4);

		public AudioFader(AudioSource sourceTemplate, CallbackService callbackService)
		{
			this.sourceTemplate = sourceTemplate;
			this.callbackService = callbackService;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public void Dispose()
		{
			for (int i = layers.Count - 1; i >= 0; i--)
			{
				layers[i].Dispose();
			}

			layers.Clear();

			if (sourceTemplate)
			{
				Object.Destroy(sourceTemplate.gameObject);
			}

			callbackService.UnsubscribeUpdates(this);
		}

		public void SetBase(AudioClip clip, TransitionSettings settings, float clipDelay = 0f, bool loop = true, float startTime = 0f, bool hidden = false)
		{
			LayerState existingBase = FindLayer(BaseLayerKey);

			if (existingBase != null && BaseMatches(existingBase, clip, loop, startTime, clipDelay))
			{
				if (hidden)
				{
					SetMaskImmediate(existingBase, 0f);
				}
				else
				{
					SetMaskImmediate(existingBase, 1f);
				}

				return;
			}

			if (existingBase != null)
			{
				RemoveLayerImmediate(existingBase);
				existingBase = null;
			}

			if (hidden)
			{
				LayerState hiddenBase = CreateLayer(
					BaseLayerKey,
					clip,
					settings,
					clipDelay,
					loop,
					startTime,
					initialMaskProgress: 0f,
					intrinsicDelay: clipDelay);

				layers.Insert(0, hiddenBase);
				return;
			}

			LayerState currentTop = GetTopLayer();
			if (currentTop != null)
			{
				// Base-to-base transition: the new base settings control both sides.
				FadeMaskOut(currentTop, settings);
				currentTop.RemoveWhenMaskSilent = true;
			}

			LayerState newBase = CreateLayer(
				BaseLayerKey,
				clip,
				settings,
				clipDelay,
				loop,
				startTime,
				initialMaskProgress: 1f,
				intrinsicDelay: clipDelay + (currentTop != null ? GetOverlapDelay(settings) : 0f));

			layers.Add(newBase);
		}

		public void PushOverride(object key, AudioClip clip, TransitionSettings settings, float clipDelay = 0f, bool loop = true, float startTime = 0f)
		{
			if (key == null)
			{
				Debug.LogError($"{nameof(AudioFader)}.{nameof(PushOverride)} called with null key.");
				return;
			}

			if (FindLayer(key) != null)
			{
				Debug.LogError($"{nameof(AudioFader)}.{nameof(PushOverride)} called with duplicate key.");
				return;
			}

			LayerState currentTop = GetTopLayer();
			if (currentTop != null)
			{
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
				intrinsicDelay: clipDelay + (currentTop != null ? GetOverlapDelay(settings) : 0f));

			layers.Add(overrideLayer);
		}

		public bool PopOverride(object key, TransitionSettings settings)
		{
			if (key == null)
			{
				Debug.LogError($"{nameof(AudioFader)}.{nameof(PopOverride)} called with null key.");
				return false;
			}

			LayerState top = GetTopLayer();
			if (top == null || !ReferenceEquals(top.Key, key))
			{
				Debug.LogError(
					$"{nameof(AudioFader)}.{nameof(PopOverride)} must pop the top override layer. " +
					$"Attempted to pop a non-top key.");
				return false;
			}

			LayerState below = GetLayerBelow(top);

			FadeMaskOut(top, settings);
			top.RemoveWhenMaskSilent = true;

			if (below != null)
			{
				FadeMaskIn(below, settings);
			}

			return true;
		}

		public void ClearOverrides()
		{
			for (int i = layers.Count - 1; i >= 0; i--)
			{
				LayerState layer = layers[i];
				if (!layer.IsBaseLayer)
				{
					RemoveLayerImmediate(layer);
				}
			}

			LayerState baseLayer = FindLayer(BaseLayerKey);
			if (baseLayer != null)
			{
				SetMaskImmediate(baseLayer, 1f);
			}
		}

		private void OnUpdate(float delta)
		{
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
			if (layer == null || layer.Source == null || layer.Clip == null)
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
			AudioSource source = Object.Instantiate(sourceTemplate);
			Object.DontDestroyOnLoad(source);

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
			layer.MaskTransition.Fill(delay: GetOverlapDelay(settings));
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

		private bool BaseMatches(LayerState layer, AudioClip clip, bool loop, float startTime, float clipDelay)
		{
			return layer != null &&
				layer.Clip == clip &&
				layer.Loop == loop &&
				Mathf.Approximately(layer.StartTime, startTime) &&
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

		private float GetOverlapDelay(TransitionSettings settings)
		{
			if (settings == null)
			{
				return 0f;
			}

			return Mathf.Max(0f, settings.OutTime) * Mathf.Clamp01(settings.RelativeDelay);
		}
	}
}
