using System.Collections;
using Gravegoods.Loot;
using Jotunn.Entities;
using Jotunn.Managers;

namespace Gravegoods.Net
{
    /// <summary>
    /// Sends the server's loot.txt to each client as it connects.
    ///
    /// Registered as Jotunn initial synchronization data. The server sends it from its
    /// RPC_PeerInfo postfix while holding back every other packet for that peer, so a
    /// client always has the tables before it receives a single ZDO, and therefore before
    /// any chest can load. ChestLoot still queues chests if that ever fails to hold.
    /// </summary>
    internal static class LootTableSync
    {
        private static CustomRPC _rpc;

        internal static void Register()
        {
            _rpc = NetworkManager.Instance.AddRPC("Gravegoods_LootTable", OnServerReceive, OnClientReceive);
            SynchronizationManager.Instance.AddInitialSynchronization(_rpc, BuildPackage);
        }

        private static ZPackage BuildPackage()
        {
            var package = new ZPackage();
            package.Write(LootTables.LocalText);
            return package;
        }

        /// <summary>Clients never send; nothing to do.</summary>
        private static IEnumerator OnServerReceive(long sender, ZPackage package)
        {
            yield break;
        }

        private static IEnumerator OnClientReceive(long sender, ZPackage package)
        {
            LootTables.ReceiveServerCopy(package.ReadString());
            ChestLoot.ProcessPending();
            yield break;
        }
    }
}
