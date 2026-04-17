using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(ParticleSystem))]
	public class ParticleSystemWrapper : MonoBehaviour
	{
		public ParticleSystem ParticleSystem => !_particleSystem ? _particleSystem = GetComponent<ParticleSystem>() : _particleSystem;
		private ParticleSystem _particleSystem;

		public float Quality { get => quality; set { quality = value; Initialize(); } }

		[SerializeField, Range(0f, 1f)] private float quality;
		[SerializeField] private Vector2 startLifetime = new Vector2(0.5f, 1f);
		[SerializeField] private Vector2 startSpeed = new Vector2(0.5f, 1f);
		[SerializeField] private Vector2 startSize = new Vector2(0.5f, 1f);

		protected void OnValidate()
		{
			Initialize();
		}

		protected void OnEnable()
		{
			Initialize();
		}

		private void Initialize()
		{
			ParticleSystem.MainModule main = ParticleSystem.main;
			main.startLifetime = new ParticleSystem.MinMaxCurve(startLifetime.Lerp(quality));
			main.startSpeed = startSpeed.Lerp(quality);
			main.startSize = new ParticleSystem.MinMaxCurve(startSize.Lerp(quality));
		}
	}
}
