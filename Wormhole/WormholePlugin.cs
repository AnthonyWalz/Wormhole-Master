using Wormhole.GridExport;
using Wormhole.Utils;
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
using System.Runtime.InteropServices.ComTypes;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;

namespace Wormhole
{
    public class Server
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string HexColor { get; set; }
        public string IP { get; set; }
        public string InFolder { get; set; }
        public string OutFolder { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public class WormholeGate
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string HexColor { get; set; }
        public string SendTo { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public class Request
    {
        public bool PluginRequest { get; set; }
        public string[] Destinations { get; set; }
        public string Destination { get; set; }
    }
    public class WormholePlugin : TorchPluginBase, IWpfPlugin, ITorchPlugin
    {

        public static WormholePlugin Instance { get; private set; }
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private GUI _control;
        public UserControl GetControl() => _control ?? (_control = new GUI(this));

        private Persistent<Config> _config;
        public Config Config => _config?.Data;
        public void Save() => _config.Save();
        public int tick = 0;
        private string admingatesfolder = "admingates";
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
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

            Instance = this;
        }

        public string CreatePath(string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName + ".sbc");
        }
        public override void Update()
        {
            base.Update();
            if (++tick == Config.Tick)
            {
                tick = 0;
                try
                {
                    if (Config.NewVersion)
                    {
                        foreach (WormholeGate wormhole in Config.WormholeGates)
                        {
                            Wormholetransferout(wormhole.Name.Trim(), wormhole.SendTo, wormhole.X, wormhole.Y, wormhole.Z);
                            Wormholetransferin(wormhole.Name.Trim(), wormhole.X, wormhole.Y, wormhole.Z);
                        }
                    }
                    else
                    {
                        foreach (Server server in Config.WormholeServer)
                        {
                            Outboundwormhole(server.IP, server.OutFolder, server.X, server.Y, server.Z);
                            Inboundwormhole(server.InFolder, server.X, server.Y, server.Z);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not run Wormhole");
                }
            }
        }
        public bool HasRightToMove(IMyPlayer player, MyCubeGrid grid)
        {
            var result = player.GetRelationTo(OwnershipUtils.GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.Owner;
            if (Config.AllowInFaction && !result)
            {
                result = player.GetRelationTo(OwnershipUtils.GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.FactionShare;
            }
            return result;
        }
        public string LegalCharOnly(string text)
        {
            string legallist = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
            string temp = "";
            foreach (char character in text)
            {
                if (legallist.IndexOf(character) != -1)
                {
                    temp = temp + character;
                }
            }
            return temp;
        }
        public void Wormholetransferout(string name, string sendto, double xgate, double ygate, double zgate)
        {
            Vector3D gatepoint = new Vector3D(xgate, ygate, zgate);
            BoundingSphereD gate = new BoundingSphereD(gatepoint, Config.RadiusGate);
            foreach (var entity in MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref gate))
            {
                var grid = (entity as IMyCubeGrid);
                if (grid != null)
                {

                    var WormholeDrives = new List<IMyJumpDrive>();
                    Log.Warn("test a");
                    var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                    gts.GetBlocksOfType(WormholeDrives);
                    if (WormholeDrives.Count > 0)
                    {
                        foreach (var WormholeDrive in WormholeDrives)
                        {
                            if (Config.JumpDriveSubid.Split(',').Any(s => s.Trim() == WormholeDrive.BlockDefinition.SubtypeId) || Config.WorkWithAllJD)
                            {
                                Request request = null;
                                try
                                {
                                    request = MyAPIGateway.Utilities.SerializeFromXML<Request>(WormholeDrive.CustomData);
                                }
                                catch { }
                                string pickeddestination = null;
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
                                {
                                    pickeddestination = sendto.Split(',')[0].Trim();
                                }
                                if (pickeddestination != null)
                                {
                                    if (WormholeDrive.IsWorking && WormholeDrive.CurrentStoredPower == WormholeDrive.MaxStoredPower)
                                    {
                                        var playerInCharge = MyAPIGateway.Players.GetPlayerControllingEntity(entity);
                                        if (playerInCharge != null && HasRightToMove(playerInCharge, entity as MyCubeGrid))
                                        {
                                            WormholeDrive.CurrentStoredPower = 0;
                                            foreach (var DisablingWormholeDrive in WormholeDrives)
                                            {
                                                if (Config.JumpDriveSubid.Split(',').Any(s => s.Trim() == DisablingWormholeDrive.BlockDefinition.SubtypeId) || Config.WorkWithAllJD)
                                                {
                                                    DisablingWormholeDrive.Enabled = false;
                                                }
                                            }
                                            List<MyCubeGrid> grids = GridFinder.FindGridList(grid.EntityId.ToString(), playerInCharge as MyCharacter, Config.IncludeConnectedGrids);
                                            if (grids == null) { return; }
                                            if (grids.Count == 0) { return; }
                                            Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(gatepoint);

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
                                                        Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(togatepoint);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                var destination = pickeddestination.Split(':');
                                                var filename = destination[0] + "_" + playerInCharge.SteamUserId.ToString() + "_" + LegalCharOnly(playerInCharge.DisplayName) + "_" + LegalCharOnly(grid.DisplayName) + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

                                                List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();
                                                foreach (MyCubeGrid mygrid in grids)
                                                {
                                                    if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                                                    {
                                                        throw new ArgumentException(mygrid + " has a ObjectBuilder thats not for a CubeGrid");
                                                    }
                                                    objectBuilders.Add(objectBuilder);
                                                }
                                                MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();
                                                definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);
                                                definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();
                                                List<ulong> playerIds = new List<ulong>();
                                                foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
                                                {
                                                    foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                                                    {

                                                        if (!Config.KeepOriginalOwner)
                                                        {
                                                            cubeBlock.Owner = 0L;
                                                            cubeBlock.BuiltBy = 0L;
                                                        }
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
                                                                var playersteam = cockpit.Pilot.PlayerSteamId;
                                                                var player = PlayerUtils.GetIdentityByNameOrId(playersteam.ToString());
                                                                playerIds.Add(playersteam);
                                                                ModCommunication.SendMessageTo(new JoinServerMessage(destination[1] + ":" + destination[2]), playersteam);
                                                            }
                                                        }
                                                    }
                                                }

                                                MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
                                                builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };
                                                foreach (var playerId in playerIds)
                                                {
                                                    var player = PlayerUtils.GetIdentityByNameOrId(playerId.ToString());
                                                    player.Character.EnableBag(false);
                                                    Sandbox.Game.MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 0);
                                                    player.Character.Close();
                                                }
                                                if (MyObjectBuilderSerializer.SerializeXML(CreatePath(Config.Folder + "/" + admingatesfolder, filename), false, builderDefinition))
                                                {
                                                    foreach (var delgrid in grids)
                                                    {
                                                        delgrid.Close();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public void Wormholetransferin(string name, double xgate, double ygate, double zgate)
        {
            Vector3D gatepoint = new Vector3D(xgate, ygate, zgate);
            BoundingSphereD gate = new BoundingSphereD(gatepoint, Config.RadiusGate);
            DirectoryInfo gridDir = new DirectoryInfo(Config.Folder + "/" + admingatesfolder);
            if (gridDir.Exists)
            {
                if (gridDir.GetFiles().Any(s => s.Name.Split('_')[0] == name))
                {
                    foreach (var file in gridDir.GetFiles())
                    {
                        if (file != null)
                        {
                            var filedataarray = file.Name.Split('_');
                            if (filedataarray[0] == name)
                            {
                                Log.Warn("yay we are going to load file: " + file.Name);
                                var player = PlayerUtils.GetIdentityByNameOrId(filedataarray[1]);
                                if (player != null || Config.KeepOriginalOwner)
                                {
                                    var playerid = 0L;
                                    if (player != null)
                                    {
                                        playerid = player.IdentityId;
                                    }
                                    if (File.Exists(file.FullName))
                                    {
                                        Log.Warn("test 1");
                                        if (MyObjectBuilderSerializer.DeserializeXML(file.FullName, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
                                        {
                                            var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;
                                            if (shipBlueprints != null)
                                            {
                                                Log.Warn("test 2");
                                                foreach (var shipBlueprint in shipBlueprints)
                                                {
                                                    var grids = shipBlueprint.CubeGrids;
                                                    if (grids != null && grids.Length > 0)
                                                    {
                                                        var pos = Utilities.FindFreePos(gate, Utilities.FindGridsRadius(grids));
                                                        if (pos != null)
                                                        {
                                                            if (Utilities.UpdateGridsPositionAndStop(grids, pos))
                                                            {
                                                                Log.Warn("test 3");
                                                                foreach (var mygrid in grids)
                                                                {
                                                                    foreach (MyObjectBuilder_CubeBlock block in mygrid.CubeBlocks)
                                                                    {
                                                                        if (!Config.KeepOriginalOwner)
                                                                        {
                                                                            block.BuiltBy = playerid;
                                                                            block.Owner = playerid;
                                                                        }
                                                                        if (block is MyObjectBuilder_Cockpit cockpit)
                                                                        {
                                                                            if (cockpit.Pilot != null)
                                                                            {
                                                                                var seatedplayerid = MyAPIGateway.Multiplayer.Players.TryGetIdentityId(cockpit.Pilot.PlayerSteamId);
                                                                                if (seatedplayerid != -1)
                                                                                {
                                                                                    Log.Warn("test 4");
                                                                                    var myplayer = MySession.Static.Players.TryGetIdentity(seatedplayerid);
                                                                                    if (seatedplayerid != -1 && Config.PlayerRespawn)
                                                                                    {
                                                                                        cockpit.Pilot.OwningPlayerIdentityId = seatedplayerid;
                                                                                        if (myplayer.Character != null)
                                                                                        {
                                                                                            if (Config.ThisIp != null && Config.ThisIp != "")
                                                                                            {
                                                                                                ModCommunication.SendMessageTo(new JoinServerMessage(Config.ThisIp), cockpit.Pilot.PlayerSteamId);
                                                                                            }
                                                                                            myplayer.Character.EnableBag(false);
                                                                                            Sandbox.Game.MyVisualScriptLogicProvider.SetPlayersHealth(seatedplayerid, 0);
                                                                                            myplayer.Character.Close();
                                                                                        }
                                                                                        myplayer.PerformFirstSpawn();
                                                                                        myplayer.SavedCharacters.Clear();
                                                                                        myplayer.SavedCharacters.Add(cockpit.Pilot.EntityId);
                                                                                        MyAPIGateway.Multiplayer.Players.SetControlledEntity(cockpit.Pilot.PlayerSteamId, cockpit.Pilot as VRage.ModAPI.IMyEntity);
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        cockpit.Pilot = null;
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    cockpit.Pilot = null;
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }

                                                                Log.Warn("test 5");
                                                                List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>(grids.ToList());
                                                                Log.Warn("test 6");
                                                                MyEntities.RemapObjectBuilderCollection(objectBuilderList);
                                                                Log.Warn("test 7");
                                                                if (!(objectBuilderList.Count > 1))
                                                                {
                                                                    foreach (var ob in objectBuilderList)
                                                                    {
                                                                        MyEntities.CreateFromObjectBuilderParallel(ob, true);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    MyStringId? temp;
                                                                    MyEntities.Load(objectBuilderList, out temp);
                                                                    Log.Warn(temp.ToString());
                                                                }
                                                                Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(gatepoint);
                                                                file.Delete();

                                                            }
                                                        }
                                                        else
                                                        {
                                                            Log.Warn("Unable to load grid no free space found at Wormhole: " + name);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public void Outboundwormhole(string ip, string outfile, double xgate, double ygate, double zgate)
        {
            BoundingSphereD gate = new BoundingSphereD(new Vector3D(xgate, ygate, zgate), Config.InRadiusGate);
            foreach (var entity in MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref gate))
            {
                var grid = (entity as VRage.Game.ModAPI.IMyCubeGrid);
                if (grid != null)
                {
                    var playerInCharge = MyAPIGateway.Players.GetPlayerControllingEntity(entity);
                    if (playerInCharge != null && OwnershipUtils.GetOwner(entity as MyCubeGrid) == playerInCharge.IdentityId)//hasrighttomove(playerInCharge, entity as MyCubeGrid))
                    {
                        var WormholeDrives = new List<IMyJumpDrive>();
                        var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                        gts.GetBlocksOfType(WormholeDrives);
                        if (Config.DontNeedJD)
                        {
                            List<MyCubeGrid> grids = GridFinder.FindGridList(grid.EntityId.ToString(), playerInCharge as MyCharacter, Config.IncludeConnectedGrids);
                            if (grids == null) { return; }
                            if (grids.Count == 0) { return; }
                            var filename = playerInCharge.SteamUserId.ToString() + "_" + grid.GetFriendlyName() + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

                            Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(new Vector3D(xgate, ygate, zgate));
                            if (GridManager.SaveGrid(CreatePath(outfile, filename), filename, ip, Config.KeepOriginalOwner, Config.ExportProjectorBlueprints, grids))
                            {
                                foreach (var delgrid in grids)
                                {
                                    delgrid.Delete();
                                }
                            }
                        }
                        else if (WormholeDrives.Count > 0)
                        {
                            foreach (var WormholeDrive in WormholeDrives)
                            {
                                if (WormholeDrive.OwnerId == playerInCharge.IdentityId && WormholeDrive.Enabled && WormholeDrive.CurrentStoredPower == WormholeDrive.MaxStoredPower && (WormholeDrive.BlockDefinition.SubtypeId.ToString() == Config.JumpDriveSubid || Config.WorkWithAllJD))
                                {
                                    WormholeDrive.CurrentStoredPower = 0;
                                    if (Config.DisableJD)
                                    {
                                        foreach (var jd in WormholeDrives)
                                        {
                                            jd.Enabled = false;
                                        }
                                    }
                                    List<MyCubeGrid> grids = GridFinder.FindGridList(grid.EntityId.ToString(), playerInCharge as MyCharacter, Config.IncludeConnectedGrids);
                                    if (grids == null) { return; }
                                    if (grids.Count == 0) { return; }
                                    var filename = playerInCharge.SteamUserId.ToString() + "_" + playerInCharge.DisplayName + "_" + grid.DisplayName + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

                                    Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(new Vector3D(xgate, ygate, zgate));
                                    if (GridManager.SaveGrid(CreatePath(outfile, filename), filename, ip, Config.KeepOriginalOwner, Config.ExportProjectorBlueprints, grids))
                                    {
                                        foreach (var delgrid in grids)
                                        {
                                            delgrid.Delete();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public void Inboundwormhole(string infile, double xgate, double ygate, double zgate)
        {
            DirectoryInfo gridDir = new DirectoryInfo(infile);
            if (gridDir.Exists)
            {
                foreach (var file in gridDir.GetFiles())
                {
                    if (file != null)
                    {
                        var player = PlayerUtils.GetIdentityByNameOrId(file.Name.Split('_')[0]);
                        if (player != null || Config.KeepOriginalOwner)
                        {
                            var playerid = -1L;
                            if (player != null)
                            {
                                playerid = player.IdentityId;
                            }
                            if (GridManager.LoadGrid(file.FullName, new BoundingSphereD(new Vector3D(xgate, ygate, zgate), Config.OutOutsideRadiusGate), Config.OutInsideRadiusGate, false, playerid, Config.KeepOriginalOwner, Config.PlayerRespawn, Config.ThisIp, false) == GridImportResult.OK)
                            {
                                Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(new Vector3D(xgate, ygate, zgate));
                                file.Delete();
                            }
                        }
                    }
                }
            }
        }
    }
}