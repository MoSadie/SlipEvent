using BepInEx;
using BepInEx.Configuration;
using System.Collections.Generic;
using HarmonyLib;
using System.Net.Http;
using BepInEx.Logging;
using UnityEngine;
using Newtonsoft.Json;
using Subpixel.Events;
using System;
using System.Net;
using RelayedMessages;
using Requests.Campaigns;
using Subpixel;
using MoCore;

namespace SlipEvent
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.mosadie.mocore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("Slipstream_Win.exe")]
    public class Plugin : BaseUnityPlugin, IMoPlugin
    {
        private static ConfigEntry<bool> httpRequestEnabled;
        private static ConfigEntry<string> httpRequestUrl;

        private static ConfigEntry<bool> streamerBotEnabled;
        private static ConfigEntry<string> streamerBotIp;
        private static ConfigEntry<int> streamerBotPort;

        private static ConfigEntry<string> streamerBotActionId;
        private static ConfigEntry<string> streamerBotActionName;

        //private static ConfigEntry<int> eventCooldown;

        private static ConfigEntry<bool> defaultCaptaincyRequired;

        private static HttpClient httpClient = new HttpClient();

        internal static ManualLogSource Log;

        private static Dictionary<EventType, ConfigEntry<int>> eventCooldownConfigs = new Dictionary<EventType, ConfigEntry<int>>();
        private static Dictionary<EventType, long> lastEventTime = new Dictionary<EventType, long>();

        public static readonly string COMPATIBLE_GAME_VERSION = "4.1595"; // Grab from log file for each game update.
        public static readonly string GAME_VERSION_URL = "https://raw.githubusercontent.com/MoSadie/SlipEvent/refs/heads/main/versions.json";

        enum CaptaincyRequiredConfigValue
        {
            Inherit,
            Required,
            NotRequired
        }

        private static Dictionary<EventType, ConfigEntry<CaptaincyRequiredConfigValue>> captaincyRequiredConfigs = new Dictionary<EventType, ConfigEntry<CaptaincyRequiredConfigValue>>();

        private void Awake()
        {
            try
            {
                Log = base.Logger;

                if (!MoCore.MoCore.RegisterPlugin(this))
                {
                    Log.LogError("Failed to register plugin with MoCore. Please check the logs for more information.");
                    return;
                }

                httpRequestEnabled = Config.Bind("HTTP", "Enabled", false, "Enable HTTP requests to a custom URL.");
                httpRequestUrl = Config.Bind("HTTP", "Url", "http://localhost:8080", "URL to send HTTP requests to.");

                streamerBotEnabled = Config.Bind("StreamerBot", "Enabled", true, "Enable StreamerBot integration.");
                streamerBotIp = Config.Bind("StreamerBot", "Ip", "127.0.0.1");
                streamerBotPort = Config.Bind("StreamerBot", "Port", 7474);
                streamerBotActionId = Config.Bind("StreamerBot", "ActionId", "da524811-ff47-4493-afe6-67f27eff234d", "Action ID to execute on game events.");
                streamerBotActionName = Config.Bind("StreamerBot", "ActionName", "(Internal) Receive Event", "Action name to execute on game events.");

                //eventCooldown = Config.Bind("StreamerBot", "EventCooldown", 5000, "Cooldown in ms before sending a duplicate event. (Cooldown is per event type.) Set to 0 to disable cooldown.");

                foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
                {
                    eventCooldownConfigs[eventType] = Config.Bind("Cooldown", $"EventCooldown_{eventType}", 0, $"Cooldown in ms before sending a duplicate {eventType} event. (ex. 5000 = 5 seconds) Set to 0 to disable cooldown.");
                }

                defaultCaptaincyRequired = Config.Bind("Captaincy", "DefaultIsCaptainRequired", false, "Configure if you must be the captain of the ship to trigger Streamer.bot actions. This sets the requirement for any event configured to 'inherit' the setting.");

                foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
                {
                    // Skip any non-ship events since no captain is possible. (For JoinShip the captain information is not available yet)
                    if (eventType == EventType.GameLaunch || eventType == EventType.GameExit || eventType == EventType.JoinShip)
                        continue;
                    captaincyRequiredConfigs[eventType] = Config.Bind("Captaincy", $"IsCaptainRequired_{eventType}", CaptaincyRequiredConfigValue.Inherit, $"Configure if you must be the captain of the ship to trigger Streamer.bot actions for the {eventType} event. (Inherit = use from the DefaultIsCaptainRequired setting , Required = must be captain, NotRequired = does not need to be captain)");
                }

                Harmony.CreateAndPatchAll(typeof(Plugin));

                // Plugin startup logic
                Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

                Svc.Get<Events>().AddListener<ShipLoadedEvent>(ShipLoadedEvent, 1);
                Svc.Get<Events>().AddListener<OrderGivenEvent>(OrderGivenEvent, 1);
                Svc.Get<Events>().AddListener<BattleStartEvent>(BattleStartEvent, 1);
                Svc.Get<Events>().AddListener<BattleEndEvent>(BattleEndEvent, 1);
                Svc.Get<Events>().AddListener<CampaignStartEvent>(CampaignStartEvent, 1);
                Svc.Get<Events>().AddListener<CampaignEndEvent>(CampaignEndEvent, 1);
                Svc.Get<Events>().AddListener<CampaignSectorChangeEvent>(CampaignSectorChangeEvent, 1);
                Svc.Get<Events>().AddListener<SectorNodeChangedEvent>(SectorNodeChangedEvent, 1);
                Svc.Get<Events>().AddListener<CrewmateCreatedEvent>(CrewmateCreatedEvent, 1);
                Svc.Get<Events>().AddListener<CrewmateRemovedEvent>(CrewmateRemovedEvent, 1);


                Application.quitting += ApplicationQuitting;

                SendEvent(EventType.GameLaunch, []);
            }
            catch (Exception e)
            {
                Logger.LogError($"An error occurred during plugin startup: {e.Message}");
            }

        }

        private enum EventType
        {
            GameLaunch,
            GameExit,
            JoinShip,
            StartFight,
            EndFight,
            NodeChange,
            ChoiceAvailable,
            CustomOrder,
            //OrderSent, // not working
            //Accolade, // not working
            KnockedOut,
            RunStarted,
            RunFailed,
            RunSucceeded,
            NextSector,
            ShopEntered,
            CrewmateCreated,
            CrewmateRemoved,
            CrewmateSwapped,
        }

        private static bool BlockEvent(EventType eventType)
        {
            Log.LogInfo($"Checking captaincy required for event {eventType} isCaptain:{GetIsCaptain()}");
            try
            {
                if (!captaincyRequiredConfigs.ContainsKey(eventType)) // Only if captaincy does not matter (Ex not on a ship)
                {
                    Log.LogDebug("Captaincy required config not found for event type. Defaulting to false.");
                    return false;
                }

                if (captaincyRequiredConfigs[eventType].Value == CaptaincyRequiredConfigValue.Inherit)
                {
                    return defaultCaptaincyRequired.Value && !GetIsCaptain();
                }
                else if (captaincyRequiredConfigs[eventType].Value == CaptaincyRequiredConfigValue.Required)
                {
                    return !GetIsCaptain();
                }
                else if (captaincyRequiredConfigs[eventType].Value == CaptaincyRequiredConfigValue.NotRequired)
                {
                    return false;
                }
                else
                {
                    Log.LogError($"Unknown captaincy required config value: {captaincyRequiredConfigs[eventType].Value}");
                    return true;
                }
            } catch (Exception e)
            {
                Log.LogError($"Error checking captaincy required for event {eventType}: {e.Message}");
                return true;
            }
        }

        private static void SendEvent(EventType eventType, Dictionary<string, string> data)
        {
            // Make an POST HTTP request to http://<streamerbotIp>:<streamerbotPort>
            // with the following data:
            // {
            //    "action": {
            //        "id": <streamerbotActionId>
            //        "name": <streamerbotActionName>
            //    },
            //    "args": {
            //        <key value pairs from data>
            //    }
            // }

            // Check if the event is blocked
            if (BlockEvent(eventType))
            {
                Log.LogInfo($"Event {eventType} is blocked. Skipping.");
                return;
            }

            // Check if the event is on cooldown
            if (eventCooldownConfigs[eventType].Value > 0)
            {
                long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (lastEventTime.ContainsKey(eventType) && currentTime - lastEventTime[eventType] < eventCooldownConfigs[eventType].Value)
                {
                    Log.LogInfo($"Event {eventType} is on cooldown. Skipping.");
                    return;
                }
                lastEventTime[eventType] = currentTime;
            }

            data.Add("eventType", eventType.ToString());

            if (streamerBotEnabled.Value)
            {
                SendEventToStreamerBot(eventType.ToString(), data);
            }

            if (httpRequestEnabled.Value)
            {
                SendEventToHttp(eventType.ToString(), data);
            }
        }

        private static void SendEventToStreamerBot(string eventType, Dictionary<string, string> data)
        {
            try
            {


                var dataJSON = JsonConvert.SerializeObject(new
                {
                    action = new
                    {
                        id = streamerBotActionId.Value,
                        name = streamerBotActionName.Value
                    },
                    args = data
                });

                Log.LogInfo($"Sending event {eventType} to StreamerBot: {streamerBotIp.Value}:{streamerBotPort.Value} with data: {dataJSON}");
                httpClient.PostAsync($"http://{streamerBotIp.Value}:{streamerBotPort.Value}/DoAction", new StringContent(dataJSON));
            }
            catch (HttpRequestException e)
            {
                Log.LogError($"Error sending event to StreamerBot: {e.Message}");
            }
        }

        private static void SendEventToHttp(string eventType, Dictionary<string, string> data)
        {
            try
            {


                var dataJSON = JsonConvert.SerializeObject(data);

                Log.LogInfo($"Sending event {eventType} to Http: {httpRequestUrl.Value} with data: {dataJSON}");
                httpClient.PostAsync(httpRequestUrl.Value, new StringContent(dataJSON));
            }
            catch (HttpRequestException e)
            {
                Log.LogError($"Error sending event to Http: {e.Message}");
            }
        }

        private void ShipLoadedEvent(ShipLoadedEvent e)
        {
            try
            {
                SendEvent(EventType.JoinShip, []);
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending ShipLoadedEvent: {ex.Message}");
            }
        }

        private void OrderGivenEvent(OrderGivenEvent e)
        {
            try
            {
                
                switch (e.Order.Type)
                {
                    // This event is only for reciving orders, not sending them.

                    //case OrderType.ACCOLADE:
                    //    sendEvent(EventType.Accolade, new Dictionary<string, string>
                    //    {
                    //        { "message", e.Order.Message }
                    //    });
                    //    break;

                    case OrderType.KnockedOut:
                        SendEvent(EventType.KnockedOut, new Dictionary<string, string>
                        {
                            { "message", e.Order.Message }
                        });
                        break;

                    case OrderType.CustomMessage:
                        string senderDisplayName = "Unknown";
                        string senderProfileImage = null;
                        bool senderIsCaptain = false;
                        MpSvc mpSvc = Svc.Get<MpSvc>();
                        if (mpSvc != null)
                        {
                            SlipClient senderClient = mpSvc.Clients.GetClientByClientId(e.Order.SenderClientId);
                            if (senderClient != null && senderClient.Player != null)
                            {
                                senderDisplayName = senderClient.Player.DisplayName ?? "Unknown";
                                senderProfileImage = senderClient.Player.ProfileImage ?? null;
                                senderIsCaptain = mpSvc.Captains.CaptainClient != null && mpSvc.Captains.CaptainClient.ClientId.Equals(senderClient.ClientId);
                            }
                        }
                        SendEvent(EventType.CustomOrder, new Dictionary<string, string>
                        {
                            { "message", e.Order.Message },
                            { "senderDisplayName", senderDisplayName },
                            { "senderProfileimage",  senderProfileImage },
                            { "senderIsCaptain", senderIsCaptain.ToString() }
                        });
                        break;

                        //case OrderType.GENERAL:
                        //case OrderType.SYSTEM_CRITICAL:
                        //case OrderType.CREW_TO_MEDBAYS:
                        //case OrderType.CREW_TO_WEAPONS:
                        //case OrderType.INVADER_ALERT:
                        //    sendEvent(EventType.OrderSent, new Dictionary<string, string>
                        //    {
                        //        { "orderType", e.Order.Type.ToString() },
                        //        { "message", e.Order.Message }
                        //    });
                        //    break;

                        //default:
                        //    Log.LogError($"Unknown order type: {e.Order.Type}");
                        //    break;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending OrderGivenEvent: {ex.Message}");
            }
        }

        private void ApplicationQuitting()
        {
            try
            {
                SendEvent(EventType.GameExit, []);
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending GameExit: {ex.Message}");
            }
        }

        private void BattleStartEvent(BattleStartEvent e)
        {
            try
            {
                if (e.Scenario == null || e.Scenario.Battle == null)
                {
                    Log.LogError("BattleStartEvent: Scenario or Battle is null");
                    return;
                }


                SendEvent(EventType.StartFight, new Dictionary<string, string>
                {
                    { "enemy", e.Scenario.Battle.Metadata.EnemyName },
                    { "invaders", e.Scenario.Battle.Metadata.InvaderDescription },
                    { "intel", e.Scenario.Battle.Metadata.IntelDescription },
                    { "threatLevel", e.Scenario.Battle.Metadata.ThreatLevel.ToString() },
                    { "speedLevel", e.Scenario.Battle.Metadata.SpeedLevel.ToString() },
                    { "cargoLevel", e.Scenario.Battle.Metadata.CargoLevel.ToString() },
                    { "medbaySpeed", e.Scenario.Stations.Medbay.HealRate.ToString() }, //FIXME: Add formatting string after determining range, likely round to int but not 100% sure
                    { "shieldSpeed", e.Scenario.Stations.Shield.ChargeRate.ToString() }, //FIXME: Add formatting string
                    { "repairSpeed", e.Scenario.Stations.Weapon.RepairRate.ToString() }, //FIXME: Add formatting string
                });
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending BattleStartEvent: {ex.Message}");
            }
        }

        private void BattleEndEvent(BattleEndEvent e)
        {
            try
            {
                SendEvent(EventType.EndFight, new Dictionary<string, string>
                {
                    { "outcome", e.Outcome.ToString() }
                });
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending BattleEndEvent: {ex.Message}");
            }
        }

        private void CampaignStartEvent(CampaignStartEvent e)
        {
            try
            {
                SendEvent(EventType.RunStarted, new Dictionary<string, string>
                {
                    { "campaign", e.Campaign.CampaignId.ToString() },
                    { "region", e.Campaign.CaptainCampaign.RegionVo.Metadata.Name }
                });
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending CampaignStartEvent: {ex.Message}");
            }
        }

        private void CampaignStartRelayed(StartCampaignRelayed e)
        {
            try
            {
                SendEvent(EventType.RunStarted, new Dictionary<string, string>
                {
                    { "campaign", e.Result.Campaign.CampaignId.ToString() },
                    { "region", e.Result.Campaign.RegionVo.Metadata.Name }
                });
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending CampaignStartRelayed: {ex.Message}");
            }
        }

        private void CampaignEndEvent(CampaignEndEvent e)
        {
            try
            {
                if (e.Victory)
                {
                    SendEvent(EventType.RunSucceeded, []);
                }
                else
                {
                    SendEvent(EventType.RunFailed, []);
                }
            } catch (Exception ex)
            {
                Log.LogError($"Error sending CampaignEndEvent: {ex.Message}");
            }
        }

        private void CampaignSectorChangeEvent(CampaignSectorChangeEvent e)
        {
            try
            {
                SendEvent(EventType.NextSector, new Dictionary<string, string>
                {
                    { "sectorIndex", e.Campaign.CurrentSectorIndex.ToString() },
                    { "sectorName", e.Campaign.CurrentSectorVo.Definition.Name }
                });
            } catch (Exception ex)
            {
                Log.LogError($"Error sending CampaignSectorChangeEvent: {ex.Message}");
            }
        }

        private void SectorNodeChangedEvent(SectorNodeChangedEvent e)
        {
            try
            {
                

                if (e.CampaignVo == null || e.CampaignVo.CurrentNodeVo.Equals(null))
                    return;

                //ScenarioWrapperVo scenario = Svc.Get<ScenarioHelpers>().GetScenarioVo(e.CampaignVo.CurrentNodeVo.Value.ScenarioKey);
                ScenarioWrapperVo scenario = e.CampaignVo.CurrentScenarioVo;

                if (scenario == null)
                {
                    Log.LogError($"Scenario not found: {e.CampaignVo.CurrentNodeVo.ScenarioKey}");
                    return;
                }
                else if (scenario.Encounter != null)
                {
                    SendEvent(EventType.ChoiceAvailable, new Dictionary<string, string>
                    {
                        { "isBacktrack", e.IsBacktrack.ToString() },
                        { "scenarioKey",  e.CampaignVo.CurrentNodeVo.ScenarioKey},
                        { "visited", e.CampaignVo.CurrentNodeVo.Visited.ToString() },
                        { "completed", e.CampaignVo.CurrentNodeVo.Completed.ToString() },
                        { "captainVictory", e.CampaignVo.CurrentNodeVo.CaptainVictory.ToString() },
                        { "scenarioName", scenario.Encounter.Name },
                        { "scenarioDescription", scenario.Encounter.Details.Full.Description },
                        { "proposition", scenario.Encounter.Proposition },
                        { "choice1", scenario.Encounter.Option1.Action },
                        { "choice2", scenario.Encounter.Option2.Action }
                    });
                }
                else if (scenario.Outpost != null)
                {
                    Dictionary<string, string> data = new Dictionary<string, string>()
                    {
                        { "isBacktrack", e.IsBacktrack.ToString() },
                        { "scenarioKey",  e.CampaignVo.CurrentNodeVo.ScenarioKey},
                        { "visited", e.CampaignVo.CurrentNodeVo.Visited.ToString() },
                        { "name", scenario.Outpost.Name },
                        { "description", scenario.Outpost.Details.Full.Description },
                        { "inventorySize", scenario.Outpost.Inventory.Length.ToString() }
                    };

                    for (int i = 0; i < scenario.Outpost.Inventory.Length; i++)
                    {
                        data.Add($"inventory{i}_type", scenario.Outpost.Inventory[i].Type.ToString());
                        data.Add($"inventory{i}_price", scenario.Outpost.Inventory[i].PricePerUnit.ToString());
                        data.Add($"inventory{i}_subtype", scenario.Outpost.Inventory[i].SubType.ToString());
                    }
                    SendEvent(EventType.ShopEntered, data);
                }

                SendEvent(EventType.NodeChange, new Dictionary<string, string>
                {
                    { "isBacktrack", e.IsBacktrack.ToString() },
                    { "scenarioKey",  e.CampaignVo.CurrentNodeVo.ScenarioKey},
                    { "visited", e.CampaignVo.CurrentNodeVo.Visited.ToString() },
                    { "completed", e.CampaignVo.CurrentNodeVo.Completed.ToString() },
                    { "captainVictory", e.CampaignVo.CurrentNodeVo.CaptainVictory.ToString() }
                });
            } catch (Exception ex)
            {
                Log.LogError($"Error sending SectorNodeChangedEvent: {ex.Message}");
            }
        }

        private void CrewmateCreatedEvent(CrewmateCreatedEvent e)
        {
            try
            {
                string name = e.Crewmate.Client != null ? e.Crewmate.Client.Player.DisplayName : "Crew";
                string playerId = e.Crewmate.Client != null ? e.Crewmate.Client.Player.SlipUserDbId : "UnknownId";


                SendEvent(EventType.CrewmateCreated, new Dictionary<string, string>
                {
                    { "name", name },
                    { "id", e.Crewmate.CrewmateId.ToString() },
                    { "playerId", playerId },
                    { "level", e.CrewmateVo.Progression.Level.ToString() },
                    { "xp", e.CrewmateVo.Progression.TotalXp.ToString() },
                    { "archetype", e.CrewmateVo.ArchetypeId },
                    { "statHealth", e.Crewmate.Stats.MaxHealth.ToString() },
                    { "statShields", e.Crewmate.Stats.MaxShields.ToString() }
                    //{ "statDefense", e.Crewmate.Stats.StationShieldChargeMultiplier.ToString() },
                    //{ "statGunnery", e.Crewmate.Stats.StationAttackMultiplier.ToString() },
                    //{ "statCombat", e.Crewmate.Stats.MeleeDamage.ToString() },
                    //{ "statRepairs", e.Crewmate.Stats.StationRepairMultiplier.ToString() },
                    //{ "statSpeed", e.Crewmate.Stats.Speed.ToString() }
                });
            } catch (Exception ex)
            {
                Log.LogError($"Error sending CrewmateCreatedEvent: {ex.Message}");
            }
        }

        private void CrewmateRemovedEvent(CrewmateRemovedEvent e)
        {
            try
            {
                string name = e.Crewmate.Client != null ? e.Crewmate.Client.Player.DisplayName : "Crew";
                string playerId = e.Crewmate.Client != null ? e.Crewmate.Client.Player.SlipUserDbId : "UnknownId";

                SendEvent(EventType.CrewmateRemoved, new Dictionary<string, string>
                {
                    { "name", name },
                    { "id", e.Crewmate.CrewmateId.ToString() },
                    { "playerId", playerId }
                });
            } catch (Exception ex)
            {
                Log.LogError($"Error sending CrewmateRemovedEvent: {ex.Message}");
            }
        }

        private static bool GetIsCaptain()
        {
            try
            {
                MpSvc mpSvc = Svc.Get<MpSvc>();

                if (mpSvc == null)
                {
                    Log.LogError("An error occurred handling self crew. null MpSvc.");
                    return false;
                }


                MpCaptainController captains = Svc.Get<MpSvc>().Captains;



                if (captains == null || captains.CaptainClient == null)
                {
                    return false;
                }
                else
                {
                    //return self.Client.Equals(captains.CaptainClient);
                    return captains.CaptainClient.IsLocal;
                }
            }
            catch (Exception e)
            {
                Log.LogError($"An error occurred while checking if the crewmate is the captain: {e.Message}");
                return false;
            }
        }


        // One day it would be nice to have a dedicated CrewmateSwappedEvent that I can listen to.
        // In the meantime I'll patch into the MpCrewController.OnCrewmateSwapped method to listen.
        [HarmonyPatch(typeof(MpCrewController), "OnCrewmateSwapped")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        static void OnCrewmateSwapped(ref MpCrewController __instance, Notification<CrewmateSwapped.Payload> notif)
        {
            //Log.LogMessage($"Crewmate Swapped Test. ID: {notif.Payload.SessionId}");
            Crewmate crewmateById = __instance.GetCrewmateById(notif.Payload.SessionId);
            if (crewmateById == null)
            {
                Log.LogError($"Crewmate not found by ID: {notif.Payload.SessionId}");
                return;
            }
            string name = crewmateById.Client != null ? crewmateById.Client.Player.DisplayName : "Crew";
            string playerId = crewmateById.Client != null ? crewmateById.Client.Player.SlipUserDbId : "UnknownId";
            try
            {
                SendEvent(EventType.CrewmateSwapped, new Dictionary<string, string>
            {
                { "name", name },
                { "id", crewmateById.CrewmateId.ToString() },
                { "playerId", playerId },
                { "level", notif.Payload.NewCrewmateVo.Progression.Level.ToString() },
                { "xp", notif.Payload.NewCrewmateVo.Progression.TotalXp.ToString() },
                { "archetype", notif.Payload.NewCrewmateVo.ArchetypeId },
                { "statHealth", notif.Payload.NewCrewmateVo.Stats.MaxHealth.ToString() },
                { "statShields", notif.Payload.NewCrewmateVo.Stats.MaxShields.ToString() }
            });
            } catch (Exception ex)
            {
                Log.LogError($"Error sending CrewmateSwappedEvent: {ex.Message}");
            }
        }

        // This is a horrible hack to fix the edge case where the First Mate starts a run,
        // causing the CampaignStartEvent to not be triggered. This may be an intended feature, unsure at the moment.
        // To fix this for now, I'm patching into the SharedCaptainConsoleManager's OnCampaignStarted method to trigger my RunStarted event.
        [HarmonyPatch(typeof(SharedCaptainConsoleManager), "OnCampaignStarted")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        static void OnRelayedCampaignStarted(ref SharedCaptainConsoleManager __instance, StartResult startResult, int clientExecutorId)
        {
            try
            {
                if (!Mainstay<CaptainReconnectManager>.Main.Equals(null) && startResult.Campaign != null && __instance.CaptainConsoleUI != null) // These are all of the if statements that are in the original method. Silently fail if any of these are false.
                {
                    Log.LogInfo("Relayed Campaign Started. Sending RunStarted event.");
                    SendEvent(EventType.RunStarted, new Dictionary<string, string>
                    {
                        { "campaign", startResult.Campaign.CampaignId.ToString() },
                        { "region", startResult.Campaign.RegionVo.Metadata.Name }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error sending relayed RunStarted: {ex.Message}");
            }
        }

        public string GetCompatibleGameVersion()
        {
            return COMPATIBLE_GAME_VERSION;
        }

        public string GetVersionCheckUrl()
        {
            return GAME_VERSION_URL;
        }

        public BaseUnityPlugin GetPluginObject()
        {
            return this;
        }

        public IMoHttpHandler GetHttpHandler()
        {
            return null;
        }
    }
}
