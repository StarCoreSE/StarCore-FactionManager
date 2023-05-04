using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;


namespace klime.SeatClear
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class SeatClear : MySessionComponentBase
    {
        List<IMyPlayer> allPlayers = new List<IMyPlayer>();
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.PlayerLeftCockpit += CockpitLeft;
                MyVisualScriptLogicProvider.PlayerEnteredCockpit += CockpitEntered;
            }
        }

        private void CockpitEntered(string entityName, long playerId, string gridName)
        {

            allPlayers.Clear();
            MyAPIGateway.Players.GetPlayers(allPlayers);

            IMyPlayer player = allPlayers.Find(x => x.IdentityId == playerId);
            if (player == null || player.Character == null) return;

            MyVisualScriptLogicProvider.ShowNotificationLocal("You will die if your cockpit is destroyed or you leave it. Be careful!", 5000, "Red");



        }

        private void CockpitLeft(string entityName, long playerId, string gridName)
        {
            allPlayers.Clear();
            MyAPIGateway.Players.GetPlayers(allPlayers);

            IMyPlayer player = allPlayers.Find(x => x.IdentityId == playerId);
            if (player == null || player.Character == null) return;

            //Add logic here for distance check etc.

            player.Character.Kill();
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.PlayerLeftCockpit -= CockpitLeft;
                MyVisualScriptLogicProvider.PlayerEnteredCockpit += CockpitEntered;
            }
        }
    }
}