﻿using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of player entities and the data belonging to them.
	/// </summary>
	public class PlayerAgentService : IService
	{
		private const string ID_PLAYER_COLLECTION = "PLAYER_ENTITIES";

		private RuntimeDataService runtimeDataService;

		public PlayerAgentService(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;
		}

		/// <summary>
		/// Will attempt to load player entity data from the <paramref name="playerIndex"/>.
		/// </summary>
		/// <param name="playerIndex">The index of player last in control on the entity.</param>
		/// <param name="data">The resulting loaded data, if any.</param>
		/// <returns>Whether retrieving the player entity data was a success.</returns>
		public bool TryRetrievePlayerEntityData(int playerIndex, out RuntimeDataCollection data)
		{
			if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				List<string> playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
				if (playerIndex < playerCollection.Count)
				{
					string id = playerCollection[playerIndex];
					if (runtimeDataService.CurrentProfile.ContainsEntry(id))
					{
						data = runtimeDataService.CurrentProfile.GetEntry<RuntimeDataCollection>(id);
						return true;
					}
				}
			}

			data = null;
			return false;
		}

		/// <summary>
		/// Mark an entity as being controlled by a player.
		/// </summary>
		/// <param name="entity">The entity now under control by a player.</param>
		/// <param name="playerIndex">The index of the player controlling the entity.</param>
		public void MarkPlayerEntity(IEntity entity, int playerIndex)
		{
			// Load player collection.
			List<string> playerCollection = new List<string>();
			if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
			}

			if (playerIndex < playerCollection.Count)
			{
				// Overwrite entity at player index.
				playerCollection[playerIndex] = entity.Identification.ID;
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}
			else
			{
				// Expand player collection.
				playerCollection.Add(entity.Identification.ID);
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}
		}
	}
}
