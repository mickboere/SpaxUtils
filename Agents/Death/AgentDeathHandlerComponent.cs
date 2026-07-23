using SpiritAxis;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentDeathHandlerComponent : AgentComponentBase
	{
		private const int DEATH_FADE_PRIO = 200;

		[SerializeField] private float dissolveDuration = 4f;
		[SerializeField, Tooltip("Duration of the fade-in when revived.")] private float resolveDuration = 1f;
		[SerializeField] private float rewardPercentage = 0.01f;
		[SerializeField] private AetherWisp wispPrefab;

		private AgentHitHandlerComponent hitHandler;
		private RigidbodyWrapper rigidbodyWrapper;
		private IHittable hittable;
		private IAgentMovementHandler movement;
		private EntityAppearanceEffectHandler appearanceEffects;
		private AetherRewardService aetherRewardService;
		private AgentStatHandler statHandler;
		private CairnService cairnService;
		private InventoryComponent inventory;
		private EquipmentComponent equipment;
		private AgentLegsComponent legs;
		private GrounderComponent grounder;

		/// <summary>When true, the dissolve still plays but the cairn + deactivation are held until <see cref="FinalizeDeath"/>
		/// is called — lets the game-over screen offer Resurrect without the body already being cairned/deactivated.</summary>
		public bool DeferDissolveFinalize { get; set; }

		private bool rewarded;
		private bool finalized;
		private EntityStat timeScale;
		private FloatOperationModifier timeScaleMod;
		private TimerClass timer;

		public void InjectDependencies(
			AgentHitHandlerComponent hitHandler,
			RigidbodyWrapper rigidbodyWrapper,
			IHittable hittable,
			IAgentMovementHandler movement,
			EntityAppearanceEffectHandler appearanceEffects,
			AetherRewardService aetherRewardService,
			AgentStatHandler statHandler,
			CairnService cairnService,
			[Optional] InventoryComponent inventory,
			[Optional] EquipmentComponent equipment,
			[Optional] GrounderComponent grounder,
			[Optional] AgentLegsComponent legs)
		{
			this.hitHandler = hitHandler;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.hittable = hittable;
			this.movement = movement;
			this.appearanceEffects = appearanceEffects;
			this.aetherRewardService = aetherRewardService;
			this.statHandler = statHandler;
			this.cairnService = cairnService;
			this.inventory = inventory;
			this.equipment = equipment;
			this.grounder = grounder;
			this.legs = legs;
		}

		protected void Awake()
		{
			Agent.DiedEvent += OnAgentDied;
			Agent.ReviveEvent += OnAgentRevived;
		}

		protected void OnDestroy()
		{
			// Dispose any running dissolve/resolve timer first: it lives on the global CallbackService and would
			// otherwise keep ticking after this agent is destroyed, firing FinalizeDeath() on a dead GameObject.
			timer?.Dispose();
			timer = null;

			Agent.DiedEvent -= OnAgentDied;
			Agent.ReviveEvent -= OnAgentRevived;
		}

		private void OnAgentDied(DeathContext deathContext)
		{
			DissolveAgent();
		}

		private void OnAgentRevived()
		{
			ResolveAgent();
		}

		#region Death animation

		private void DissolveAgent()
		{
			// Agent died, disable movement, gravity, and hittability, slow down time and start fade out.
			hittable.IsHittable = false;
			rigidbodyWrapper.Control.AddModifier(this, new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 0f));
			movement.InputRaw = Vector3.zero;
			movement.InputSmooth = Vector3.zero;
			movement.AutoUpdateMovement = false;
			movement.AutoUpdateRotation = false;
			if (grounder) grounder.Ground = false;
			if (legs)
			{
				foreach (Leg leg in legs.Legs)
				{
					leg.UpdateGround(false, 0f, false, default, default);
				}
			}

			timeScale = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			timeScaleMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			timeScale.AddModifier(this, timeScaleMod);

			timer?.Dispose();
			timer = new TimerClass(dissolveDuration, 1f, true);
			timer.UpdateEvent += OnTimerUpdate;

			appearanceEffects.RequestFade(this, DEATH_FADE_PRIO, 1f, 0f);
		}

		private void ResolveAgent()
		{
			// Revive at the last safe location instead of where we died (a pit, hazard or void) — same position the
			// cairn uses. Done before the fade-in below, which starts fully faded out, so the move is never seen.
			// The grounder picks the jump up on its own and re-bases its smoothed state accordingly.
			if (grounder != null)
			{
				rigidbodyWrapper.Position = grounder.LastSafePosition;
			}

			// Clear momentum from the death: TargetVelocity survives the death (AutoUpdateMovement was off, so nothing
			// decayed it), and would otherwise lurch the agent off in its dying direction the moment movement resumes.
			rigidbodyWrapper.ResetVelocity();
			rigidbodyWrapper.TargetVelocity = Vector3.zero;

			// Its a miracle! The agent returned from the dead, re-enable everything.
			hittable.IsHittable = true;
			rigidbodyWrapper.Control.RemoveModifier(this);
			movement.AutoUpdateMovement = true;
			movement.AutoUpdateRotation = true;
			if (grounder) grounder.Ground = true;

			// Null-safe: a resolve may run without a preceding dissolve having set these up.
			timeScale?.RemoveModifier(this);
			timeScaleMod?.Dispose();
			timeScaleMod = null;

			// Fade the agent back in gradually rather than snapping visible.
			timer?.Dispose();
			timer = new TimerClass(resolveDuration, 1f, true);
			timer.UpdateEvent += OnResolveTimerUpdate;

			// NOTE: DeferDissolveFinalize is a policy owned by the game-over screen, not per-death state — do NOT
			// reset it here. Clearing it meant a re-death relied on the screen's OnEnable re-firing to set it again;
			// if the screen never disabled in between, the next dissolve would finalize (cairn + SetActive(false)),
			// leaving the player deactivated and invisible.
			finalized = false;
		}

		private void OnTimerUpdate(float delta)
		{
			timeScaleMod.SetValue(timer.Progress.InvertClamped().InQuad());

			appearanceEffects.RequestFade(this, DEATH_FADE_PRIO, 1f, timer.Progress.Clamp01());

			if (timer.Progress >= 0.5f)
			{
				// Halfway through the fade spawn the reward.
				HandleReward();
			}

			if (timer.Expired)
			{
				timer.Dispose();
				timer = null;
				OnAgentDissolved();
			}
		}

		private void OnResolveTimerUpdate(float delta)
		{
			// Fade alpha from invisible (1) back to visible (0) over the resolve duration.
			appearanceEffects.RequestFade(this, DEATH_FADE_PRIO, 1f, timer.Progress.InvertClamped());

			if (timer.Expired)
			{
				timer.Dispose();
				timer = null;
				appearanceEffects.Clear(this);
			}
		}

		private void OnAgentDissolved()
		{
			if (DeferDissolveFinalize)
			{
				// Held for the game-over screen to decide (resurrect vs respawn/reload); it calls FinalizeDeath() on choice.
				return;
			}

			FinalizeDeath();
		}

		/// <summary>Registers the cairn (player only) and deactivates the agent. Deferred while the game-over screen gates the flow.</summary>
		public void FinalizeDeath()
		{
			if (finalized)
			{
				return;
			}
			finalized = true;

			// Stop the dissolve if it is still running (e.g. respawn picked before it completes).
			if (timer != null)
			{
				timer.Dispose();
				timer = null;
			}

			if (Agent.Identification.HasAny(EntityLabels.PLAYER))
			{
				// Only register a cairn if agent is a player.
				RegisterCairn();
			}

			// Fully faded, deactivate.
			Agent.GameObject.SetActive(false);
		}

		#endregion Death animation

		#region Reward

		private void HandleReward()
		{
			if (rewarded) return;

			string playerId = PlayerAgentService.GetPlayerId(0);
			if (hitHandler.DamageLedger.ContainsKey(playerId))
			{
				// Reward Aether to player through a whisp. Priced off one level's cost AT THIS AGENT'S RANK, not
				// its whole EXP budget — the budget grows a full power faster than a level does, which made kills
				// pay for progressively more levels the higher you ranked. rewardPercentage now reads directly as
				// "fraction of a level per equal-rank kill".
				Agent.Stats.TryGetStat(AgentStatIdentifiers.BODY_RANK, out EntityStat bodyRank);
				float reward = SpaxFormulas.LevelUpCost(SpaxFormulas.PointsFromRank(bodyRank.Value)) * rewardPercentage;
				aetherRewardService.Reward(Agent.Targetable.Center, playerId, reward);
			}

			rewarded = true;
		}

		#endregion Reward

		#region Cairn

		private void RegisterCairn()
		{
			// The soul caps off the body; all surplus EXP is lost.
			statHandler.ResetToSoul(out RuntimeDataCollection lost);

			// Own the data:
			lost.SetValue(EntityDataIdentifiers.ID, Agent.ID);

			// Judge the owner at the moment of death: alignment becomes the cairn's starting scaling,
			// so a saint's cairn rises at full health while a sinner's rises fully degraded.
			float sin = Agent.Stats.TryGetStat(AgentStatIdentifiers.SIN, out EntityStat sinStat) ? sinStat.Value : 100f;
			float virtue = Agent.Stats.TryGetStat(AgentStatIdentifiers.VIRTUE, out EntityStat virtueStat) ? virtueStat.Value : 100f;
			lost.SetValue(EntityDataIdentifiers.ALIGNMENT, SpaxFormulas.GetAlignment(sin, virtue));

			// Collect material items from inventory and place them in lost data.
			if (inventory != null)
			{
				List<string> materialItemKeys = new List<string>();
				RuntimeDataCollection materialInventory = new RuntimeDataCollection(InventoryComponent.INVENTORY_DATA_ID, parent: lost);
				foreach (KeyValuePair<string, RuntimeItemData> kvp in inventory.Inventory.Entries)
				{
					if (!kvp.Value.RuntimeData.TryGetValue(ItemDataIdentifiers.AETHERIAL, out bool aetherial) || !aetherial)
					{
						materialItemKeys.Add(kvp.Key);

						// Clone item data and add to lost inventory.
						RuntimeDataCollection itemData = kvp.Value.RuntimeData.CloneCollection();
						materialInventory.TryAdd(itemData);

						// If an item was equiped, add an equiped bool entry to the item data, so that it can be equiped again when retrieved.
						if (equipment != null)
						{
							foreach (RuntimeEquipedData equipedItem in equipment.EquipedItems)
							{
								if (equipedItem.RuntimeItemData == kvp.Value)
								{
									itemData.SetValue(ItemDataIdentifiers.EQUIPED, true);
									break;
								}
							}
						}
					}
				}

				// Remove all material items from inventory.
				foreach (string key in materialItemKeys)
				{
					inventory.Inventory.RemoveItem(key);
				}
			}

			// Retrieve last safe position to place cairn at.
			Vector3 pos = grounder != null ? grounder.LastSafePosition : Agent.Transform.position;

			// Register cairn with cairn service, providing lost data and position.
			cairnService.RegisterCairn(Agent, pos, lost);

			// Make sure changes are reflected in save profile.
			Agent.SaveData();
		}

		#endregion Cairn
	}
}
