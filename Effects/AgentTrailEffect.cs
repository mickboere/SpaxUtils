using System.Collections.Generic;
using UnityEngine;

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
		}

		[SerializeField] private Material trailMaterial;
		[SerializeField] private float interval = 0.05f;
		[SerializeField] private float duration = 0.5f;

		private EntityAppearanceHandler entityAppearanceHandler;

		private readonly List<Mesh> meshPool = new List<Mesh>();
		private readonly List<Snapshot> activeSnapshots = new List<Snapshot>();
		private readonly List<CombineInstance> combineInstances = new List<CombineInstance>(16);

		private MaterialPropertyBlock propertyBlock;
		private Mesh combinedMesh;

		private float timeSinceLastSnapshot;
		private bool capturing;

		public void InjectDependencies(EntityAppearanceHandler entityAppearanceHandler)
		{
			this.entityAppearanceHandler = entityAppearanceHandler;
		}

		protected void Start()
		{
			propertyBlock = new MaterialPropertyBlock();

			int maxSnapshots = (duration / interval).CeilToInt();
			for (int i = 0; i < maxSnapshots; i++)
			{
				meshPool.Add(new Mesh());
			}
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
				timeSinceLastSnapshot += Time.deltaTime;
				if (timeSinceLastSnapshot >= interval)
				{
					timeSinceLastSnapshot = 0f;
					CaptureSnapshot();
				}
			}

			DrawAndCleanupSnapshots();
		}

		public void Begin()
		{
			capturing = true;
			timeSinceLastSnapshot = 0f;
		}

		public void End()
		{
			capturing = false;
		}

		private void EnsureMeshPoolSize(int required)
		{
			while (meshPool.Count < required)
			{
				meshPool.Add(new Mesh());
			}
		}

		private void CaptureSnapshot()
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
			activeSnapshots.Add(new Snapshot
			{
				Mesh = snapshotMesh,
				StartTime = Time.time
			});
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

				float alpha = 1f - (age / duration);
				propertyBlock.SetFloat("_Alpha", alpha);

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
