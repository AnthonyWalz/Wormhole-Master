using Wormhole.GridExport;
using Wormhole.Utils;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
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
using Sandbox.Game.World;
using Sandbox.Engine.Multiplayer;

namespace Wormhole
{       
    public class Server
    {
        public string Name { get; set; }
        public string IP { get; set; }
        public string InFolder { get; set; }
        public string OutFolder { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public class WormholePlugin : TorchPluginBase, IWpfPlugin
    {

            public static WormholePlugin Instance { get; private set; }
            public static readonly Logger Log = LogManager.GetCurrentClassLogger();

            private Control _control;
            public UserControl GetControl() => _control ?? (_control = new Control(this));

            private Persistent<Config> _config;
            public Config Config => _config?.Data;
            public void Save() => _config.Save();
            public int ticknumber = 0;

            public int tick()
            {
                return ++ticknumber;
            }
            public void tickreset()
            {
                ticknumber = 0;
            }
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
                if (tick() == Config.Tick) {
                    tickreset();
                    try
                    {
                        foreach (Server server in Config.WormholeServer)
                        {
                            outboundwormhole(server.IP, server.OutFolder, server.X, server.Y, server.Z);
                            inboundwormhole(server.InFolder, server.X, server.Y, server.Z);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Could not run Wormhole");
                    }
                }
            }
            public bool hasrighttomove(IMyPlayer player, MyCubeGrid grid)
            {
                var result = player.GetRelationTo(OwnershipUtils.GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.Owner;
                if (Config.AllowInFaction) {
                    result = result && player.GetRelationTo(OwnershipUtils.GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.FactionShare;
                }
                return result;
            }
            public void outboundwormhole(string ip, string outfile, double xgate, double ygate, double zgate)
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
                            if (WormholeDrives.Count > 0)
                            {
                                foreach (var WormholeDrive in WormholeDrives)
                                {
                                    if (WormholeDrive.OwnerId == playerInCharge.IdentityId && WormholeDrive.Enabled && WormholeDrive.CurrentStoredPower == WormholeDrive.MaxStoredPower && WormholeDrive.BlockDefinition.SubtypeId.ToString() == Config.JumpDriveSubid)
                                    {
                                        WormholeDrive.CurrentStoredPower = 0;

                                        List<MyCubeGrid> grids = GridFinder.FindGridList(grid.EntityId.ToString(), playerInCharge as MyCharacter, Config.IncludeConnectedGrids);
                                        if (grids == null) { return; }
                                        if (grids.Count == 0) { return; }
                                        var filename = playerInCharge.SteamUserId.ToString() + "_" + grid.EntityId.ToString() + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
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
            public void inboundwormhole(string infile, double xgate, double ygate, double zgate)
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
                                if (GridManager.LoadGrid(file.FullName, new BoundingSphereD(new Vector3D(xgate, ygate, zgate), Config.OutOutsideRadiusGate), Config.OutInsideRadiusGate, false, playerid, Config.KeepOriginalOwner, Config.PlayerRespawn, false) == GridImportResult.OK)
                                {
                                    file.Delete();
                                }
                            }
                        }
                    }
                }
            }
        }
    }