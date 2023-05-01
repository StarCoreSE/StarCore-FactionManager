using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;
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

    //copy the above method but rename DelayedFaction to DelayedPlayer
    public class DelayedPlayer
    {
        public IMyPlayer player;
        public long playerid;

        public DelayedPlayer(long playerid)
        {
            this.playerid = playerid;
        }
    }


    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class FactionManager : MySessionComponentBase
    {
        public List<IMyPlayer> allPlayers = new List<IMyPlayer>();
        public string factionButtonSubtype = "FactionButton";
        public List<string> reuseSections = new List<string>();
        public List<DelayedFaction> delayedFactions = new List<DelayedFaction>();
        public List<DelayedPlayer> delayedPlayers = new List<DelayedPlayer>();
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
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            }
        }

        private void PlayerConnected(long playerid)
        {

            DelayedPlayer player = new DelayedPlayer(playerid);
            delayedPlayers.Add(player);


        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.StartsWith("/rebalance"))
            {
                var player = MyAPIGateway.Session.Player;
                if (player == null || MyPromoteLevel.Admin >= MyAPIGateway.Session.Player.PromoteLevel)
                {
                    MyAPIGateway.Utilities.ShowMessage("Error", "You must be an admin to use this command.");
                    return;
                }

                Rebalance();
            }
        }



        private void Rebalance()
        {
            MyAPIGateway.Utilities.ShowNotification("Rebalancing, dumbass", 2000, MyFontEnum.Green);
            IMyFaction redfaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("RED");
            IMyFaction blufaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("BLU");

            if (redfaction != null && blufaction != null)
            {
                var redcount = redfaction.Members.Count;
                var blucount = blufaction.Members.Count;

                if (redcount != blucount)
                {
                    var memberIds = new List<long>();
                    var fromFaction = (redcount > blucount) ? redfaction : blufaction;
                    var toFaction = (redcount > blucount) ? blufaction : redfaction;

                    foreach (var member in fromFaction.Members)
                    {
                        if (!member.Value.IsFounder)
                        {
                            memberIds.Add(member.Key);
                        }
                    }

                    var diff = Math.Abs(redcount - blucount);
                    var membersToMove = diff / 2;

                    for (var i = 0; i < membersToMove; i++)
                    {
                        if (memberIds.Count == 0)
                        {
                            // no more members to move
                            break;
                        }

                        var memberId = memberIds[MyUtils.GetRandomInt(0, memberIds.Count - 1)];
                        MyAPIGateway.Session.Factions.SendJoinRequest(toFaction.FactionId, memberId);
                        MyAPIGateway.Session.Factions.AcceptJoin(toFaction.FactionId, memberId);
                        memberIds.Remove(memberId);
                    }
                }
                else
                {
                    // equal count, do nothing
                }
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

                for (int i = delayedPlayers.Count - 1; i >= 0; i--)
                {
                    var name = MyVisualScriptLogicProvider.GetPlayersName(delayedPlayers[i].playerid);
                    if (name != "")
                    {
                        IMyFaction redfaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("RED");
                        IMyFaction blufaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("BLU");
                        if (redfaction != null && blufaction != null)
                        {
                            var redcount = redfaction.Members.Count;
                            var blucount = blufaction.Members.Count;
                            if (redcount > blucount)
                            {
                                //Add them to new faction
                                MyAPIGateway.Session.Factions.SendJoinRequest(blufaction.FactionId, delayedPlayers[i].playerid);
                                MyAPIGateway.Session.Factions.AcceptJoin(blufaction.FactionId, delayedPlayers[i].playerid);
                            }
                            else if (blucount > redcount)
                            {
                                //Add them to new faction
                                MyAPIGateway.Session.Factions.SendJoinRequest(redfaction.FactionId, delayedPlayers[i].playerid);
                                MyAPIGateway.Session.Factions.AcceptJoin(redfaction.FactionId, delayedPlayers[i].playerid);
                            }
                            else //oh god oh fuck it failed put them on red
                            {
                                MyAPIGateway.Session.Factions.SendJoinRequest(redfaction.FactionId, delayedPlayers[i].playerid);
                                MyAPIGateway.Session.Factions.AcceptJoin(redfaction.FactionId, delayedPlayers[i].playerid);
                            }

                            delayedPlayers.RemoveAt(i); //this player has been processed send them to the bin

                        }
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
                MyIni ini = new MyIni(); ;
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
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
                MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
            }
        }
    }
}