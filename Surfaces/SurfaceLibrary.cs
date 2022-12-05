using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "SurfaceLibrary", menuName = "ScriptableObjects/SurfaceLibrary")]
	public class SurfaceLibrary : ScriptableObject, IService
	{
		[SerializeField] private List<SurfaceConfiguration> surfaces;

		public SurfaceConfiguration Get(string surface)
		{
			return surfaces.FirstOrDefault((s) => s.Surface == surface);
		}
	}

	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;
		public float Hardness => hardness;
		public float MediumImpact => mediumImpact;
		public float StrongImpact => strongImpact;
		public SurfaceAudioData Audio => audio;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField, Range(0f, 1f)] private float hardness = 1f;
		[SerializeField] private float mediumImpact = 10f;
		[SerializeField] private float strongImpact = 100f;
		[SerializeField] private SurfaceAudioData audio;

		public float CalculateImpact(float force)
		{
			float impact = force * hardness;
			return impact;
		}

		public ImpactAudioData GetImpactAudio(float force, bool calculateImpact = true, bool requireClips = true)
		{
			float impact = calculateImpact ? CalculateImpact(force) : force;
			ImpactStrength strength = GetImpactStrength(impact);
			ImpactAudioData data = audio.GetImpactAudioData(strength);

			if (requireClips)
			{
				while (data.Clips.Count < 1 && (int)strength > 0)
				{
					strength = (ImpactStrength)((int)strength - 1);
					data = audio.GetImpactAudioData(strength);
				}
			}

			return data;
		}

		public ImpactStrength GetImpactStrength(float impact)
		{
			if (impact >= mediumImpact && impact < strongImpact)
			{
				return ImpactStrength.Medium;
			}
			else if (impact >= strongImpact)
			{
				return ImpactStrength.Strong;
			}
			return ImpactStrength.Light;
		}
	}

	public enum ImpactStrength
	{
		Light = 0,
		Medium = 1,
		Strong = 2
	}

	[Serializable]
	public class SurfaceAudioData
	{
		public ImpactAudioData Light => light;
		public ImpactAudioData Medium => medium;
		public ImpactAudioData Strong => strong;
		public ImpactAudioData Long => prolonged;

		[SerializeField, FormerlySerializedAs("lightImpact")] private ImpactAudioData light;
		[SerializeField, FormerlySerializedAs("mediumImpact")] private ImpactAudioData medium;
		[SerializeField, FormerlySerializedAs("strongImpact")] private ImpactAudioData strong;
		[SerializeField, FormerlySerializedAs("longImpact")] private ImpactAudioData prolonged;

		public ImpactAudioData GetImpactAudioData(ImpactStrength strength)
		{
			switch (strength)
			{
				case ImpactStrength.Medium:
					return Medium;
				case ImpactStrength.Strong:
					return Strong;
				default:
					return Light;
			}
		}
	}

	[Serializable]
	public class ImpactAudioData
	{
		public IReadOnlyList<AudioClip> Clips => clips;
		public AudioClip RandomClip => clips[UnityEngine.Random.Range(0, clips.Count)];

		public Vector2 VolumeRange => volumeRange;
		public float RandomVolume => UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
		public Vector2 PitchRange => pitchRange;
		public float RandomPitch => UnityEngine.Random.Range(pitchRange.x, pitchRange.y);

		[SerializeField] private List<AudioClip> clips;
		[SerializeField, MinMaxRange(0.01f, 1f, true)] private Vector2 volumeRange = new Vector2(1f, 1f);
		[SerializeField] private AnimationCurve volumeCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, MinMaxRange(0.01f, 3f, true, false)] private Vector2 pitchRange = new Vector2(1f, 1f);
		[SerializeField] private AnimationCurve pitchCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

		public float EvaluateVolume(float t, bool useCurve = true)
		{
			if (useCurve)
			{
				return Mathf.Lerp(volumeRange.x, volumeRange.y, volumeCurve.Evaluate(t));
			}
			else
			{
				return Mathf.Lerp(volumeRange.x, volumeRange.y, t);
			}
		}

		public float EvaluatePitch(float t, bool useCurve = true)
		{
			if (useCurve)
			{
				return Mathf.Lerp(pitchRange.x, pitchRange.y, pitchCurve.Evaluate(t));
			}
			else
			{
				return Mathf.Lerp(pitchRange.x, pitchRange.y, t);
			}
		}
	}
}
