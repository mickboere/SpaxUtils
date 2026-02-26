using NUnit.Framework;
using SpiritAxis;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentDeathHandlerComponent : AgentComponentBase
	{
		private const int DEATH_FADE_PRIO = 200;

		[SerializeField] private float dissolveDuration = 4f;
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

		private bool rewarded;
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

			timer = new TimerClass(dissolveDuration, 1f, true);
			timer.UpdateEvent += OnTimerUpdate;

			appearanceEffects.RequestFade(this, DEATH_FADE_PRIO, 1f, 0f);
		}

		private void ResolveAgent()
		{
			// Its a miracle! The agent returned from the dead, re-enable everything.
			hittable.IsHittable = true;
			rigidbodyWrapper.Control.RemoveModifier(this);
			movement.AutoUpdateMovement = true;
			movement.AutoUpdateRotation = true;
			if (grounder) grounder.Ground = true;

			timeScale.RemoveModifier(this);
			timeScaleMod.Dispose();

			timer?.Dispose();

			appearanceEffects.Clear(this);
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
				OnAgentDissolved();
			}
		}

		private void OnAgentDissolved()
		{
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
				// Reward Aether to player through a whisp.
				Agent.Stats.TryGetStat(AgentStatIdentifiers.BODY_RANK, out EntityStat bodyRank);
				float reward = SpaxFormulas.PointsFromRank(bodyRank.Value) * rewardPercentage;
				aetherRewardService.Reward(Agent.Targetable.Center, playerId, reward);
			}

			rewarded = true;
		}

		#endregion Reward

		#region Cairn

		private void RegisterCairn()
		{
			// Reset agent's stats back to backup.
			statHandler.ResetToBackup(out RuntimeDataCollection lost);

			// Own the data:
			lost.SetValue(EntityDataIdentifiers.ID, Agent.ID);

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
