using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using VRageMath;
using VRage.Game;
using VRage.Game.ModAPI;
using System.Linq;
using VRage.ObjectBuilders;
using Sandbox.Common.ObjectBuilders;
using Torch.Mod;
using Torch.Mod.Messages;
using Sandbox.Game.World;
using Sandbox.Game;

namespace Wormhole
{
    public class WormholePlugin : TorchPluginBase, IWpfPlugin
    {
        public static WormholePlugin Instance { get; private set; }
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private GUI _control;
        public UserControl GetControl() => _control ?? (_control = new GUI(this));

        private Persistent<Config> _config;
        public Config Config => _config?.Data;
        public void Save() => _config.Save();

        private int tick = 0;

        public string admingatesfolder = "admingates";
        public string admingatesconfirmfolder = "admingatesconfirm";
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            SetupConfig();
        }

        private void SetupConfig()
        {
            var configFile = Path.Combine(StoragePath, "Wormhole.cfg");
            try
            {
                _config = Persistent<Config>.Load(configFile);
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
            {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<Config>(configFile, new Config());
                _config.Save();
            }
        }

        public override void Update()
        {
            base.Update();
            if (++tick == Config.Tick)
            {
                tick = 0;
                try
                {
                    foreach (WormholeGate wormhole in Config.WormholeGates)
                    {
                        Vector3D gatepoint = new Vector3D(wormhole.X, wormhole.Y, wormhole.Z);
                        BoundingSphereD gate = new BoundingSphereD(gatepoint, Config.RadiusGate);
                        Wormholetransferout(wormhole.SendTo, gatepoint, gate);
                        Wormholetransferin(wormhole.Name.Trim(), gatepoint, gate);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not run Wormhole");
                }
            }
        }


        public void Wormholetransferout(string sendto, Vector3D gatepoint, BoundingSphereD gate)
        {
            foreach (var grid in MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref gate).OfType<IMyCubeGrid>())
            {
                var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);

                if (gts != null)
                {
                    var WormholeDrives = new List<IMyJumpDrive>();
                    gts.GetBlocksOfType(WormholeDrives);

                    foreach (var WormholeDrive in WormholeDrives)
                    {
                        WormholeTransferOutFile(sendto, grid, WormholeDrive, gatepoint, WormholeDrives);
                    }
                }
            }
        }

        private void WormholeTransferOutFile(string sendto, IMyCubeGrid grid, IMyJumpDrive WormholeDrive, Vector3D gatepoint, List<IMyJumpDrive> WormholeDrives)
        {
            if (!Config.JumpDriveSubid.Split(',').Any(s => s.Trim() == WormholeDrive.BlockDefinition.SubtypeId) && !Config.WorkWithAllJD)
                return;

            Request request = default;
            try
            {
                request = MyAPIGateway.Utilities.SerializeFromXML<Request>(WormholeDrive.CustomData);
            }
            catch { }

            string pickeddestination = default;

            if (request != null)
            {
                if (request.PluginRequest)
                {
                    if (request.Destination != null)
                    {
                        if (sendto.Split(',').Any(s => s.Trim() == request.Destination.Trim()))
                        {
                            pickeddestination = request.Destination.Trim();
                        }
                    }
                    Request reply = new Request
                    {
                        PluginRequest = false,
                        Destination = null,
                        Destinations = sendto.Split(',').Select(s => s.Trim()).ToArray()
                    };
                    WormholeDrive.CustomData = MyAPIGateway.Utilities.SerializeToXML<Request>(reply);
                }
            }
            else
            {
                Request reply = new Request
                {
                    PluginRequest = false,
                    Destination = null,
                    Destinations = sendto.Split(',').Select(s => s.Trim()).ToArray()
                };
                WormholeDrive.CustomData = MyAPIGateway.Utilities.SerializeToXML<Request>(reply);
            }

            if (Config.AutoSend && sendto.Split(',').Length == 1)
                pickeddestination = sendto.Split(',')[0].Trim();

            if (pickeddestination == null)
                return;

            if (!WormholeDrive.IsWorking || WormholeDrive.CurrentStoredPower != WormholeDrive.MaxStoredPower)
                return;

            var playerInCharge = MyAPIGateway.Players.GetPlayerControllingEntity(grid);
            if (playerInCharge == null || !Utilities.HasRightToMove(playerInCharge, grid as MyCubeGrid))
                return;

            WormholeDrive.CurrentStoredPower = 0;
            foreach (var DisablingWormholeDrive in WormholeDrives)
            {
                if (Config.JumpDriveSubid.Split(',').Any(s => s.Trim() == DisablingWormholeDrive.BlockDefinition.SubtypeId) || Config.WorkWithAllJD)
                {
                    DisablingWormholeDrive.Enabled = false;
                }
            }
            List<MyCubeGrid> grids = Utilities.FindGridList(grid.EntityId.ToString(), playerInCharge as MyCharacter, Config.IncludeConnectedGrids);

            if (grids == null)
                return;

            if (grids.Count == 0)
                return;

            MyVisualScriptLogicProvider.CreateLightning(gatepoint);

            //NEED TO DROP ENEMY GRIDS
            if (Config.WormholeGates.Any(s => s.Name.Trim() == pickeddestination.Split(':')[0]))
            {
                foreach (WormholeGate internalwormhole in Config.WormholeGates)
                {
                    if (internalwormhole.Name.Trim() == pickeddestination.Split(':')[0].Trim())
                    {
                        var box = WormholeDrive.GetTopMostParent().WorldAABB;
                        var togatepoint = new Vector3D(internalwormhole.X, internalwormhole.Y, internalwormhole.Z);
                        var togate = new BoundingSphereD(togatepoint, Config.RadiusGate);
                        Utilities.UpdateGridsPositionAndStopLive(WormholeDrive.GetTopMostParent(), Utilities.FindFreePos(togate, (float)(Vector3D.Distance(box.Center, box.Max) + 50)));
                        MyVisualScriptLogicProvider.CreateLightning(togatepoint);
                    }
                }
            }
            else
            {
                var destination = pickeddestination.Split(':');

                var transferFileInfo = new Utilities.TransferFileInfo
                {
                    destinationWormhole = destination[0],
                    steamUserId = playerInCharge.SteamUserId,
                    playerName = playerInCharge.DisplayName,
                    gridName = grid.DisplayName,
                    time = DateTime.Now
                };

                Log.Info("creating filetransfer:" + transferFileInfo.ToString());
                var filename = transferFileInfo.createFileName();

                List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();
                foreach (MyCubeGrid mygrid in grids)
                {
                    if (!(mygrid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                    {
                        throw new ArgumentException(mygrid + " has a ObjectBuilder thats not for a CubeGrid");
                    }
                    objectBuilders.Add(objectBuilder);
                }
                MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

                definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);

                definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();

                List<ulong> playerSteamIds = new List<ulong>();
                foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
                {
                    foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                    {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                        if (!Config.ExportProjectorBlueprints)
                        {
                            if (cubeBlock is MyObjectBuilder_ProjectorBase projector)
                            {
                                projector.ProjectedGrids = null;
                            }
                        }
                        if (cubeBlock is MyObjectBuilder_Cockpit cockpit)
                        {
                            if (cockpit.Pilot != null)
                            {
                                var playerSteamId = cockpit.Pilot.PlayerSteamId;
                                playerSteamIds.Add(playerSteamId);
                                ModCommunication.SendMessageTo(new JoinServerMessage(destination[1] + ":" + destination[2]), playerSteamId);
                            }
                        }
                    }
                }

                MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
                builderDefinition.ShipBlueprints = new[] { definition };
                foreach (var playerSteamId in playerSteamIds)
                {
                    var player = Utilities.GetIdentityBySteamId(playerSteamId);
                    player.Character.EnableBag(false);
                    MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 0);
                    player.Character.Close();
                }
                if (MyObjectBuilderSerializer.SerializeXML(Utilities.CreateBlueprintPath(Path.Combine(Config.Folder, admingatesfolder), filename), false, builderDefinition))
                    grids.ForEach(b => b.Close());
            }
        }
        public void Wormholetransferin(string wormholeName, Vector3D gatepoint, BoundingSphereD gate)
        {
            DirectoryInfo gridDir = new DirectoryInfo(Config.Folder + "/" + admingatesfolder);

            if (!gridDir.Exists)
                return;

            if (!gridDir.GetFiles().Any(s => s.Name.Split('_')[0] == wormholeName))
                return;

            foreach (var file in gridDir.GetFiles())
            {
                if (file != null && File.Exists(file.FullName))
                {
                    var fileTransferInfo = Utilities.TransferFileInfo.parseFileName(file.Name);
                    if (fileTransferInfo.HasValue)
                    {
                        if (wormholeName != fileTransferInfo.Value.destinationWormhole)
                        {
                            WormholeTransferInFile(file, fileTransferInfo.Value, gatepoint, gate);
                        }
                    }
                }
            }
        }

        private void WormholeTransferInFile(FileInfo fileInfo, Utilities.TransferFileInfo fileTransferInfo, Vector3D gatePosition, BoundingSphereD gate)
        {
            Log.Info("processing filetransfer:" + fileTransferInfo.ToString());

            var player = Utilities.GetIdentityBySteamId(fileTransferInfo.steamUserId);
            if (player == null)
            {
                Log.Error("couldn't find player with steam id: " + fileTransferInfo.steamUserId);
                return;
            }

            var playerid = player.IdentityId;

            if (!MyObjectBuilderSerializer.DeserializeXML(fileInfo.FullName, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
            {
                Log.Error("error deserializing xml: " + fileInfo.FullName);
                return;
            }

            var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;
            if (shipBlueprints == null)
            {
                Log.Error("can't find any blueprints in xml: " + fileInfo.FullName);
                return;
            }

            foreach (var shipBlueprint in shipBlueprints)
            {
                var grids = shipBlueprint.CubeGrids;
                if (grids == null || grids.Length == 0)
                    continue;

                var pos = Utilities.FindFreePos(gate, Utilities.FindGridsRadius(grids));
                if (pos == null)
                {
                    Log.Warn("no free space available for grid '" + shipBlueprint.DisplayName + "' at wormhole '" + fileTransferInfo.destinationWormhole + "'");
                    continue;
                }

                if (Utilities.UpdateGridsPositionAndStop(grids, pos))
                {
                    foreach (var mygrid in grids)
                    {
                        foreach (MyObjectBuilder_CubeBlock block in mygrid.CubeBlocks)
                        {
                            block.BuiltBy = playerid;
                            block.Owner = playerid;

                            if (!(block is MyObjectBuilder_Cockpit cockpit) || cockpit.Pilot == null)
                                continue;

                            var seatedplayerid = MyAPIGateway.Multiplayer.Players.TryGetIdentityId(cockpit.Pilot.PlayerSteamId);
                            if (seatedplayerid == -1)
                            {
                                cockpit.Pilot = null;
                                continue;
                            }

                            var myplayer = MySession.Static.Players.TryGetIdentity(seatedplayerid);
                            if (seatedplayerid == -1 || !Config.PlayerRespawn)
                            {
                                cockpit.Pilot = null;
                                continue;
                            }
                            cockpit.Pilot.OwningPlayerIdentityId = seatedplayerid;
                            if (myplayer.Character != null)
                            {
                                if (Config.ThisIp != null && Config.ThisIp != "")
                                {
                                    ModCommunication.SendMessageTo(new JoinServerMessage(Config.ThisIp), cockpit.Pilot.PlayerSteamId);
                                }
                                myplayer.Character.EnableBag(false);
                                MyVisualScriptLogicProvider.SetPlayersHealth(seatedplayerid, 0);
                                myplayer.Character.Close();
                            }
                            myplayer.PerformFirstSpawn();
                            myplayer.SavedCharacters.Clear();
                            myplayer.SavedCharacters.Add(cockpit.Pilot.EntityId);
                            MyAPIGateway.Multiplayer.Players.SetControlledEntity(cockpit.Pilot.PlayerSteamId, cockpit.Pilot as VRage.ModAPI.IMyEntity);
                        }
                    }
                }

                List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>(grids.ToList());
                MyEntities.RemapObjectBuilderCollection(objectBuilderList);
                if (objectBuilderList.Count > 1)
                {
                    if (MyEntities.Load(objectBuilderList, out _))
                    {
                        fileInfo.Delete();
                    }
                    else
                    {
                        foreach (var ob in objectBuilderList)
                        {
                            if (MyEntities.CreateFromObjectBuilderParallel(ob, true) != null)
                            {
                                fileInfo.Delete();
                            }
                        }
                    }
                }
                MyVisualScriptLogicProvider.CreateLightning(gatePosition);
            }
        }
    }
}