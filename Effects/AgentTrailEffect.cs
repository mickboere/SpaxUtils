using System.Collections.Generic;
using UnityEngine;
using SpiritAxis;

namespace SpaxUtils
{
	/// <summary>
	/// Captures and draws a trail of the Agent's skinned meshes during motion.
	/// Combines all active non-cloth SkinnedMeshRenderers into a single mesh snapshot
	/// for efficient rendering with fading alpha over time.
	/// </summary>
	public class AgentTrailEffect : AgentComponentBase
	{
		private struct Snapshot
		{
			public Mesh Mesh;
			public float StartTime;
			public DeformSmear Smear;
			public float PinFloor;
			public float PinHeight;
		}

		private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
		private const float MATCHED_FALLOFF = 1000f;

		public Material TrailMaterial => trailMaterial;
		public float Spacing => spacing;
		public float Duration => duration;

		[SerializeField] private Material trailMaterial;
		[SerializeField, Tooltip("Meters travelled between snapshots.")] private float spacing = 0.75f;
		[SerializeField] private float duration = 0.5f;

		private EntityAppearanceHandler entityAppearanceHandler;
		private EntityAppearanceEffectHandler appearanceEffects;

		private readonly List<Mesh> meshPool = new List<Mesh>();
		private readonly List<Snapshot> activeSnapshots = new List<Snapshot>();
		private readonly List<CombineInstance> combineInstances = new List<CombineInstance>(16);

		private MaterialPropertyBlock propertyBlock;
		private Mesh combinedMesh;

		private Vector3 lastCapturePosition;
		private bool matchSpacing;
		private float matchReach = 1f;
		private bool capturing;
		private TrailSmearMode smearMode;

		public void InjectDependencies(EntityAppearanceHandler entityAppearanceHandler,
			[Optional] EntityAppearanceEffectHandler appearanceEffects)
		{
			this.entityAppearanceHandler = entityAppearanceHandler;
			this.appearanceEffects = appearanceEffects;
		}

		protected void Start()
		{
			propertyBlock = new MaterialPropertyBlock();

			EnsureMeshPoolSize(8);
		}

		protected void OnDestroy()
		{
			for (int i = 0; i < meshPool.Count; i++)
			{
				Object.Destroy(meshPool[i]);
			}

			for (int i = 0; i < activeSnapshots.Count; i++)
			{
				Object.Destroy(activeSnapshots[i].Mesh);
			}

			if (combinedMesh != null)
			{
				Object.Destroy(combinedMesh);
			}
		}

		protected void Update()
		{
			if (capturing)
			{
				// Distance, not time: even spacing at any speed, so snapshots can be smeared into each other.
				Vector3 travel = transform.position - lastCapturePosition;
				if (travel.sqrMagnitude >= spacing * spacing)
				{
					CaptureSnapshot(travel);
					lastCapturePosition = transform.position;
				}
			}

			DrawAndCleanupSnapshots();
		}

		/// <summary>
		/// Starts capturing; <paramref name="smearMode"/> decides how snapshots carry the body's smear.
		/// <paramref name="matchSpacing"/> stretches each smear back to the previous snapshot (× <paramref name="reach"/>).
		/// </summary>
		public void Begin(TrailSmearMode smearMode = TrailSmearMode.None, bool matchSpacing = false, float reach = 1f)
		{
			this.smearMode = smearMode;
			this.matchSpacing = matchSpacing;
			matchReach = reach;
			capturing = true;
			lastCapturePosition = transform.position;
		}

		public void End()
		{
			capturing = false;
		}

		/// <summary>
		/// Fade and smear of a snapshot <paramref name="age"/> seconds old; shared with the editor preview.
		/// </summary>
		public static void SetSnapshotProperties(MaterialPropertyBlock mpb, DeformSmear smear,
			float pinFloor, float pinHeight, float age, float duration)
		{
			mpb.SetFloat(AlphaId, 1f - age / Mathf.Max(duration, 0.0001f));
			smear.Phase += age * smear.Scroll;
			MaterialEffectRenderer.SetSmearProperties(mpb, smear, pinFloor, pinHeight);
		}

		/// <summary>
		/// Stretches a snapshot smear back along <paramref name="travel"/> × <paramref name="reach"/>, ignoring falloff.
		/// </summary>
		public static void MatchSpacing(ref DeformSmear smear, Vector3 travel, float reach)
		{
			smear.Vector = -travel * reach;
			smear.Falloff = MATCHED_FALLOFF;
		}

		private void EnsureMeshPoolSize(int required)
		{
			while (meshPool.Count < required)
			{
				meshPool.Add(new Mesh());
			}
		}

		private void CaptureSnapshot(Vector3 travel)
		{
			List<SkinnedMeshRenderer> renderers = entityAppearanceHandler.ActiveRenderers;
			if (renderers == null || renderers.Count == 0)
			{
				return;
			}

			EnsureMeshPoolSize(renderers.Count);
			combineInstances.Clear();

			for (int i = 0; i < renderers.Count; i++)
			{
				SkinnedMeshRenderer smr = renderers[i];
				if (smr == null || smr.TryGetComponent<Cloth>(out _))
				{
					// Skip cloth or invalid renderers.
					continue;
				}

				Mesh mesh = meshPool[meshPool.Count - 1];
				meshPool.RemoveAt(meshPool.Count - 1);

				smr.BakeMesh(mesh);
				combineInstances.Add(new CombineInstance
				{
					mesh = mesh,
					transform = smr.transform.localToWorldMatrix
				});
			}

			if (combineInstances.Count == 0)
			{
				return; // nothing to combine
			}

			if (combinedMesh == null)
			{
				combinedMesh = new Mesh();
			}
			else
			{
				combinedMesh.Clear();
			}

			combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

			// Return baked meshes to pool
			for (int i = 0; i < combineInstances.Count; i++)
			{
				meshPool.Add(combineInstances[i].mesh);
			}

			Mesh snapshotMesh = Object.Instantiate(combinedMesh);
			Snapshot snapshot = new Snapshot
			{
				Mesh = snapshotMesh,
				StartTime = Time.time,
				PinHeight = 1f,
			};

			if (smearMode != TrailSmearMode.None && appearanceEffects != null)
			{
				appearanceEffects.GetSmear(out snapshot.Smear, out snapshot.PinFloor, out snapshot.PinHeight);
				// Snapshot streaks run from the smear's phase at capture, not the render clock.
				if (smearMode == TrailSmearMode.Frozen)
				{
					snapshot.Smear.Scroll = 0f;
				}
				if (matchSpacing)
				{
					MatchSpacing(ref snapshot.Smear, travel, matchReach);
				}
			}

			activeSnapshots.Add(snapshot);
		}

		private void DrawAndCleanupSnapshots()
		{
			for (int i = activeSnapshots.Count - 1; i >= 0; i--)
			{
				Snapshot snapshot = activeSnapshots[i];
				float age = Time.time - snapshot.StartTime;

				if (age >= duration)
				{
					Object.Destroy(snapshot.Mesh);
					activeSnapshots.RemoveAt(i);
					continue;
				}

				SetSnapshotProperties(propertyBlock, snapshot.Smear, snapshot.PinFloor, snapshot.PinHeight, age, duration);

				Graphics.DrawMesh(
					snapshot.Mesh,
					Matrix4x4.identity,
					trailMaterial,
					gameObject.layer,
					null,
					0,
					propertyBlock
				);
			}
		}
	}
}
