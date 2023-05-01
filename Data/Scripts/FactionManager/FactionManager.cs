using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;


namespace klime.FactionManager
{
    public class DelayedFaction
    {
        public IMyFaction faction;
        public int runtime;

        public DelayedFaction(IMyFaction faction, int runtime)
        {
            this.faction = faction;
            this.runtime = runtime;
        }
    }


    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class FactionManager : MySessionComponentBase
    {
        public List<IMyPlayer> allPlayers = new List<IMyPlayer>();
        public string factionButtonSubtype = "FactionButton";
        public List<string> reuseSections = new List<string>();
        public List<DelayedFaction> delayedFactions = new List<DelayedFaction>();
        public List<string> specialFactions = new List<string>()
        {
            "SPRT",
            "SPID"
        };

        //Core
        public int masterTimer = 0;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName += ButtonPressed;
                MyAPIGateway.Session.Factions.FactionStateChanged += FactionEvent;
                MyAPIGateway.Session.Factions.FactionCreated += FactionCreated;
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                for (int i = delayedFactions.Count - 1; i >= 0; i--)
                {
                    if (masterTimer > delayedFactions[i].runtime)
                    {
                        bool validFaction = false;

                        if (delayedFactions[i].faction.IsEveryoneNpc())
                        {
                            validFaction = true;
                        }

                        if (IsAdmin(delayedFactions[i].faction.FounderId))
                        {
                            validFaction = true;
                        }

                        if (!validFaction)
                        {
                            foreach (var member in delayedFactions[i].faction.Members.Keys)
                            {
                                MyAPIGateway.Session.Factions.KickMember(delayedFactions[i].faction.FactionId, member);
                            }
                        }
                        delayedFactions.RemoveAt(i);
                    }
                }
            }

            masterTimer += 1;
        }

        private void FactionCreated(long factionId)
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);
            if (faction != null)
            {
                DelayedFaction delayedFaction = new DelayedFaction(faction, masterTimer + 60);
                delayedFactions.Add(delayedFaction);
            }
        }

        private void ButtonPressed(string name, int buttonNum, long playerId, long blockId)
        {
            IMyButtonPanel buttonPanel = MyAPIGateway.Entities.GetEntityById(blockId) as IMyButtonPanel;

            if (buttonPanel != null && buttonPanel.BlockDefinition.SubtypeName == factionButtonSubtype)
            {
                reuseSections.Clear();
                MyIni ini = new MyIni();;
                string tag = "";

                if (ini.TryParse(buttonPanel.CustomData))
                {
                    ini.GetSections(reuseSections);
                    if (reuseSections.Count == 1)
                    {
                        tag = reuseSections[0];
                    }
                }

                if (!string.IsNullOrEmpty(tag) && tag != "")
                {
                    var factionToAdd = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag);
                    if (factionToAdd != null)
                    {
                        string existingTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(playerId);

                        if (existingTag == "" || existingTag == "SPRT" || existingTag == "SPID")
                        {
                            //Kick from existing faction
                            MyVisualScriptLogicProvider.KickPlayerFromFaction(playerId);

                            //Add them to new faction
                            MyAPIGateway.Session.Factions.SendJoinRequest(factionToAdd.FactionId, playerId);
                            MyAPIGateway.Session.Factions.AcceptJoin(factionToAdd.FactionId, playerId);
                        }
                    }
                }
            }
        }

        private void FactionEvent(MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId)
        {
            //Admin check
            if (IsAdmin(senderId))
            {
                return;
            }

            //Revert standard apply
            if (action == MyFactionStateChange.FactionMemberSendJoin)
            {
                if (senderId != 0)
                {
                    MyAPIGateway.Session.Factions.CancelJoinRequest(fromFactionId, playerId);
                }
            }

            //Revert leave
            if (action == MyFactionStateChange.FactionMemberLeave)
            {
                if (senderId != 0)
                {
                    MyAPIGateway.Session.Factions.SendJoinRequest(fromFactionId, playerId);
                    MyAPIGateway.Session.Factions.AcceptJoin(fromFactionId, playerId);
                }
            }

            //Revert kick
            if (action == MyFactionStateChange.FactionMemberKick)
            {
                if (senderId != 0)
                {
                    MyAPIGateway.Session.Factions.SendJoinRequest(fromFactionId, playerId);
                    MyAPIGateway.Session.Factions.AcceptJoin(fromFactionId, playerId);
                }
            }
        }

        private bool IsAdmin(long identityId)
        {
            bool retVal = false;

            allPlayers.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(allPlayers);
            IMyPlayer testPlayer = null;

            foreach (var p in allPlayers)
            {
                if (p.IdentityId == identityId)
                {
                    testPlayer = p;
                    break;
                }
            }

            if (testPlayer != null)
            {
                if (testPlayer.PromoteLevel == MyPromoteLevel.Admin || testPlayer.PromoteLevel == MyPromoteLevel.Owner)
                {
                    retVal = true;
                }
            }

            return retVal;
        }

        //private IMyPlayer GetPlayer(long identityId)
        //{
        //    IMyPlayer retPlayer = null;
        //    allPlayers.Clear();
        //    MyAPIGateway.Multiplayer.Players.GetPlayers(allPlayers);

        //    foreach (var p in allPlayers)
        //    {
        //        if (p.IdentityId == identityId)
        //        {
        //            retPlayer = p;
        //            break;
        //        }
        //    }

        //    return retPlayer;
        //}

        protected override void UnloadData()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyVisualScriptLogicProvider.ButtonPressedTerminalName -= ButtonPressed;
                MyAPIGateway.Session.Factions.FactionStateChanged -= FactionEvent;
                MyAPIGateway.Session.Factions.FactionCreated -= FactionCreated;
            }
        }
    }
}