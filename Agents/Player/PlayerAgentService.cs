using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of player entities and the data belonging to them.
	/// </summary>
	public class PlayerAgentService : IService
	{
		private const string ID_PLAYER_COLLECTION = "PLAYER_ENTITIES";

		/// <summary>
		/// All currently marked player agents.
		/// </summary>
		public IReadOnlyDictionary<int, IAgent> Agents => _agents;
		private Dictionary<int, IAgent> _agents = new Dictionary<int, IAgent>();

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
		/// Mark an agent as being controlled by a player.
		/// </summary>
		/// <param name="agent">The agent currently under control by player <paramref name="playerIndex"/>.</param>
		/// <param name="playerIndex">The index of the player controlling the agent.</param>
		public void MarkPlayerAgent(IAgent agent, int playerIndex)
		{
			_agents[playerIndex] = agent;

			// Load player collection.
			List<string> playerCollection = new List<string>();
			if (runtimeDataService.CurrentProfile.ContainsEntry(ID_PLAYER_COLLECTION))
			{
				playerCollection = runtimeDataService.CurrentProfile.GetValue<List<string>>(ID_PLAYER_COLLECTION);
			}

			if (playerIndex < playerCollection.Count)
			{
				// Overwrite entity at player index.
				playerCollection[playerIndex] = agent.Identification.ID;
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}
			else
			{
				// Expand player collection.
				playerCollection.Add(agent.Identification.ID);
				runtimeDataService.CurrentProfile.SetValue(ID_PLAYER_COLLECTION, playerCollection);
			}
		}
	}
}
