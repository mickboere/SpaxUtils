using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Minimal service for reserving Aether rewards and later claiming/canceling them.
	/// Reservations are removed from memory when claimed or canceled.
	/// </summary>
	public class AetherRewardService : IService
	{
		public sealed class AetherReservation
		{
			public string ReservationId { get; private set; }
			public IAgent Target { get; private set; }
			public float Amount { get; private set; }

			public AetherReservation(string reservationId, IAgent target, float amount)
			{
				ReservationId = reservationId;
				Amount = amount;
				Target = target;
			}
		}

		private IEntityCollection entityCollection;
		private Pool<AetherWisp> wispPool;

		private readonly Dictionary<string, AetherReservation> reservations = new Dictionary<string, AetherReservation>();

		public AetherRewardService(IEntityCollection entityCollection, Pool<AetherWisp> wispPool)
		{
			this.entityCollection = entityCollection;
			this.wispPool = wispPool;
		}

		/// <summary>
		/// Spawn an Aether whisp that will apply the reward on arrival.
		/// </summary>
		public void Reward(Vector3 origin, string targetID, float amount)
		{
			AetherReservation reservation = Reserve(targetID, amount);
			wispPool.Request(origin, null, (whisp) =>
			{
				reservation.Target.DependencyManager.Inject(whisp);
				whisp.Initialize(reservation);
			});
		}

		/// <summary>
		/// Create a reservation for an Aether reward to be claimed later.
		/// </summary>
		public AetherReservation Reserve(string targetID, float amount)
		{
			if (targetID.IsNullOrEmpty())
			{
				SpaxDebug.Error("Can't reserve Aether.", "Target is null.");
				return null;
			}

			if (amount <= 0f)
			{
				SpaxDebug.Error("Can't reserve Aether.", "Amount must be > 0.");
				return null;
			}

			if (!entityCollection.TryGet(targetID, out IAgent target))
			{
				SpaxDebug.Error("Can't reserve Aether.", $"No agent was found with ID '{targetID}'.");
				return null;
			}

			string reservationId = Guid.NewGuid().ToString();
			AetherReservation reservation = new AetherReservation(reservationId, target, amount);
			reservations.Add(reservationId, reservation);

			return reservation;
		}

		/// <summary>
		/// Claim a reservation and apply the Aether to the target.
		/// On success, the reservation is removed.
		/// Returns false if reservation doesn't exist or couldn't be applied.
		/// </summary>
		public bool Claim(string reservationId)
		{
			if (string.IsNullOrEmpty(reservationId) ||
				!reservations.TryGetValue(reservationId, out AetherReservation reservation))
			{
				return false;
			}

			// Reward.
			reservation.Target.Stats.GetStat(AgentStatIdentifiers.AETHER, true, 0f).BaseValue += reservation.Amount;
			reservations.Remove(reservationId);
			return true;
		}

		/// <summary>
		/// Cancel a reservation without applying it.
		/// On success, the reservation is removed.
		/// </summary>
		public bool Cancel(string reservationId)
		{
			if (string.IsNullOrEmpty(reservationId))
			{
				return false;
			}

			return reservations.Remove(reservationId);
		}

		/// <summary>
		/// Clear all reservations.
		/// </summary>
		public void Clear()
		{
			reservations.Clear();
		}
	}
}
