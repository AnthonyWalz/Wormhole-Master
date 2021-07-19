using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using NLog;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game;
using VRage.Game.ObjectBuilder;
using VRage.ObjectBuilders;
using VRageMath;

namespace Wormhole
{
    public class WormholePlugin : TorchPluginBase, IWpfPlugin
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<ulong, (Utilities.TransferFileInfo, TransferFile, Vector3D, BoundingSphereD)>
            _queuedSpawns = new();

        private Persistent<Config> _config;

        private Gui _control;
        public const string AdminGatesConfig = "admingatesconfig";
        private const string AdminGatesConfirmReceivedFolder = "admingatesconfirmreceived";
        private const string AdminGatesConfirmSentFolder = "admingatesconfirmsent";

        public const string AdminGatesFolder = "admingates";
        private Task _saveOnEnterTask;

        // The actual task of saving the game on exit or enter
        private Task _saveOnExitTask;

        private int _tick;
        
        private string _gridDir;
        private string _gridDirSent;
        private string _gridDirReceived;
        private readonly XmlSerializer _requestSerializer = new(typeof(Request));

        public static WormholePlugin Instance { get; private set; }
        public Config Config => _config?.Data;

        public UserControl GetControl() => _control ??= new(this);

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            SetupConfig();
            Torch.GameStateChanged += (_, state) =>
            {
                if (state == TorchGameState.Loaded)
                    MyMultiplayer.Static.ClientJoined += MultiplayerOnClientJoined;
            };
        }

        private void MultiplayerOnClientJoined(ulong clientId, string name)
        {
            if (!_queuedSpawns.ContainsKey(clientId))
                return;
            
            _queuedSpawns.TryRemove(clientId, out var tuple);
            var (transferFileInfo, transferFile, gamePoint, gate) = tuple;
            try
            {
                WormholeTransferInQueue(transferFile, transferFileInfo, gamePoint, gate);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not run queued spawn for {name} ({clientId})");
            }
        }

        public override void Update()
        {
            base.Update();
            if (++_tick != Config.Tick) return;
            _tick = 0;
            try
            {
                foreach (var wormhole in Config.WormholeGates)
                {
                    var gatePoint = new Vector3D(wormhole.X, wormhole.Y, wormhole.Z);
                    var gate = new BoundingSphereD(gatePoint, Config.RadiusGate);
                    WormholeTransferOut(wormhole.SendTo, gatePoint, gate);
                    WormholeTransferIn(wormhole.Name.Trim(), gatePoint, gate);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not run Wormhole");
            }

            try
            {
                //check transfer status
                foreach (var file in Directory.EnumerateFiles(_gridDirReceived, "*.sbc"))
                    //if all other files have been correctly removed then remove safety to stop duplication
                {
                    var fileName = Path.GetFileName(file);
                    if (!File.Exists(Path.Combine(_gridDirSent, fileName)) &&
                        !File.Exists(Path.Combine(_gridDir, fileName)))
                        File.Delete(file);
                }
            }
            catch
            {
                //no issue file might in deletion process
            }
        }

        public void Save()
        {
            _config.Save();
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
        }


        public void WormholeTransferOut(string sendTo, Vector3D gatePoint, BoundingSphereD gate)
        {
            foreach (var grid in MyEntities.GetTopMostEntitiesInSphere(ref gate).OfType<MyCubeGrid>())
            {
                var gts = grid.GridSystems.TerminalSystem;
                if (gts == null)
                    continue;

                var jumpDrives = gts.Blocks.OfType<MyJumpDrive>().ToList();

                foreach (var jumpDrive in jumpDrives)
                    WormholeTransferOutFile(sendTo, grid, jumpDrive, gatePoint, jumpDrives);
            }
        }

        private void WormholeTransferOutFile(string sendTo, MyCubeGrid grid, MyJumpDrive wormholeDrive,
            Vector3D gatePoint, IEnumerable<MyJumpDrive> wormholeDrives)
        {
            if (Config.JumpDriveSubid.Split(',').All(s => s.Trim() != wormholeDrive.BlockDefinition.Id.SubtypeName) &&
                !Config.WorkWithAllJd)
                return;

            var reply = new Request
            {
                PluginRequest = false,
                Destination = null,
                Destinations = sendTo.Split(',').Select(s => s.Trim()).ToArray()
            };

            string pickedDestination = default;
            if (!string.IsNullOrEmpty(wormholeDrive.CustomData))
            {
                var request = (Request)_requestSerializer.Deserialize(new StringReader(wormholeDrive.CustomData));
                if (request is {PluginRequest: true, Destination: { }})
                {
                    if (sendTo.Split(',').Any(s => s.Trim() == request.Destination.Trim())) 
                        pickedDestination = request.Destination.Trim();
                }
            }

            using (var writer = new StringWriter())
            {
                _requestSerializer.Serialize(writer, reply);
                wormholeDrive.CustomData = writer.ToString();
            }

            if (Config.AutoSend && sendTo.Split(',').Length == 1)
                pickedDestination = sendTo.Split(',')[0].Trim();

            if (pickedDestination == null)
                return;

            var playerInCharge = Sync.Players.GetControllingPlayer(grid);

            if (playerInCharge?.Identity is null || !wormholeDrive.CanJumpAndHasAccess(playerInCharge.Identity.IdentityId) ||
                !Utilities.HasRightToMove(playerInCharge, grid))
                return;

            wormholeDrive.CurrentStoredPower = 0;
            foreach (var disablingWormholeDrive in wormholeDrives)
                if (Config.JumpDriveSubid.Split(',')
                        .Any(s => s.Trim() == disablingWormholeDrive.BlockDefinition.Id.SubtypeName) ||
                    Config.WorkWithAllJd)
                    disablingWormholeDrive.Enabled = false;
            var grids = Utilities.FindGridList(grid.EntityId.ToString(), playerInCharge.Character,
                Config.IncludeConnectedGrids);

            if (grids == null)
                return;

            if (grids.Count == 0)
                return;

            MyVisualScriptLogicProvider.CreateLightning(gatePoint);

            //NEED TO DROP ENEMY GRIDS
            if (Config.WormholeGates.Any(s => s.Name.Trim() == pickedDestination.Split(':')[0]))
            {
                foreach (var internalWormhole in Config.WormholeGates)
                    if (internalWormhole.Name.Trim() == pickedDestination.Split(':')[0].Trim())
                    {
                        var box = wormholeDrive.GetTopMostParent().PositionComp.WorldAABB;
                        var toGatePoint = new Vector3D(internalWormhole.X, internalWormhole.Y, internalWormhole.Z);
                        var toGate = new BoundingSphereD(toGatePoint, Config.RadiusGate);
                        Utilities.UpdateGridsPositionAndStopLive(wormholeDrive.GetTopMostParent(),
                            Utilities.FindFreePos(toGate, (float) (Vector3D.Distance(box.Center, box.Max) + 50)) ??
                            Vector3D.Zero);
                        MyVisualScriptLogicProvider.CreateLightning(toGatePoint);
                    }
            }
            else
            {
                var destination = pickedDestination.Split(':');

                if (3 != destination.Length)
                    throw new ArgumentException("failed parsing destination '" + destination + "'");

                var transferFileInfo = new Utilities.TransferFileInfo
                {
                    DestinationWormhole = destination[0],
                    SteamUserId = playerInCharge.Id.SteamId,
                    PlayerName = playerInCharge.DisplayName,
                    GridName = grid.DisplayName,
                    Time = DateTime.Now
                };

                Log.Info("creating filetransfer:" + transferFileInfo.CreateLogString());
                var filename = transferFileInfo.CreateFileName();

                var objectBuilders = new List<MyObjectBuilder_CubeGrid>();
                foreach (var mygrid in grids)
                {
                    if (mygrid.GetObjectBuilder() is not MyObjectBuilder_CubeGrid objectBuilder)
                        throw new ArgumentException(mygrid + " has a ObjectBuilder thats not for a CubeGrid");
                    objectBuilders.Add(objectBuilder);
                }

                static IEnumerable<long> GetIds(MyObjectBuilder_CubeBlock block)
                {
                    if (block.Owner > 0)
                        yield return block.Owner;
                    if (block.BuiltBy > 0)
                        yield return block.BuiltBy;
                }

                var identitiesMap = objectBuilders.SelectMany(static b => b.CubeBlocks)
                    .SelectMany(GetIds).Distinct().Where(static b => !Sync.Players.IdentityIsNpc(b))
                    .ToDictionary(static b => b, static b => Sync.Players.TryGetIdentity(b).GetObjectBuilder());

                var sittingPlayerIdentityIds = new HashSet<long>();
                foreach (var cubeBlock in objectBuilders.SelectMany(static cubeGrid => cubeGrid.CubeBlocks))
                {
                    if (!Config.ExportProjectorBlueprints)
                        if (cubeBlock is MyObjectBuilder_ProjectorBase projector)
                            projector.ProjectedGrids = null;
                    
                    if (cubeBlock is not MyObjectBuilder_Cockpit cockpit) continue;
                    if (cockpit.Pilot?.OwningPlayerIdentityId == null) continue;
                    
                    var playerSteamId = Sync.Players.TryGetSteamId(cockpit.Pilot.OwningPlayerIdentityId.Value);
                    sittingPlayerIdentityIds.Add(cockpit.Pilot.OwningPlayerIdentityId.Value);
                    ModCommunication.SendMessageTo(new JoinServerMessage(destination[1] + ":" + destination[2]),
                        playerSteamId);
                }

                using (var stream = File.Create(Utilities.CreateBlueprintPath(Path.Combine(Config.Folder, AdminGatesFolder), filename)))
                        Serializer.Serialize(stream, new TransferFile
                        {
                            Grids = objectBuilders,
                            IdentitiesMap = identitiesMap,
                            PlayerIdsMap = identitiesMap.Select(static b =>
                            {
                                Sync.Players.TryGetPlayerId(b.Key, out var id);
                                return (b.Key, id.SteamId);
                            }).Where(static b => b.SteamId > 0)
                                .ToDictionary(static b => b.Key, static b => b.SteamId)
                        });

                foreach (var identity in sittingPlayerIdentityIds.Select(Sync.Players.TryGetIdentity)
                    .Where(b => b.Character is { })) KillCharacter(identity.Character);
                foreach (var cubeGrid in grids)
                {
                    cubeGrid.Close();
                }
                // Saves the game if enabled in config.
                if (Config.SaveOnExit)
                {
                    // (re)Starts the task if it has never been started o´r is done
                    if (_saveOnExitTask is null || _saveOnExitTask.IsCompleted)
                        _saveOnExitTask = Torch.Save();
                }

                File.Create(Utilities.CreateBlueprintPath(_gridDirSent, filename)).Dispose();
            }
        }

        public void WormholeTransferIn(string wormholeName, Vector3D gatePoint, BoundingSphereD gate)
        {
            EnsureDirectoriesCreated();

            var changes = false;

            foreach (var file in Directory.EnumerateFiles(_gridDir, "*.sbc")
                    .Where(s => Path.GetFileNameWithoutExtension(s).Split('_')[0] == wormholeName))
                //if file not null if file exists if file is done being sent and if file hasnt been received before
            {
                var fileName = Path.GetFileName(file);
                if (!File.Exists(file) || !File.Exists(Path.Combine(_gridDirSent, fileName)) ||
                    File.Exists(Path.Combine(_gridDirReceived, fileName))) continue;
                
                Log.Info("Processing recivied grid: " + fileName);
                var fileTransferInfo = Utilities.TransferFileInfo.ParseFileName(fileName);
                if (fileTransferInfo is null)
                {
                    Log.Error("Error parsing file name");
                    continue;
                }
                    
                TransferFile transferFile;
                try
                {
                    using var stream = File.OpenRead(file);
                    transferFile = Serializer.Deserialize<TransferFile>(stream);
                    if (transferFile.Grids is null || transferFile.IdentitiesMap is null)
                        throw new InvalidOperationException("File is empty or invalid");
                }
                catch (Exception e)
                {
                    Log.Error(e, $"Corrupted file at {fileName}");
                    continue;
                }

                // if player online, process now, else queue util player join
                if (Sync.Players.TryGetPlayerBySteamId(fileTransferInfo.SteamUserId) is { })
                    WormholeTransferInQueue(transferFile, fileTransferInfo, gatePoint, gate);
                else
                    _queuedSpawns[fileTransferInfo.SteamUserId] = (fileTransferInfo, transferFile, gatePoint, gate);

                changes = true;
                File.Delete(Path.Combine(_gridDirSent, fileName));
                File.Delete(Path.Combine(_gridDir, fileName));
                File.Create(Path.Combine(_gridDirReceived, fileName)).Dispose();
            }

            // Saves game on enter if enabled in config.
            if (!changes || !Config.SaveOnEnter) return;
            
            if (_saveOnEnterTask is null || _saveOnEnterTask.IsCompleted)
                _saveOnEnterTask = Torch.Save();
        }

        private void WormholeTransferInQueue(TransferFile file, Utilities.TransferFileInfo fileTransferInfo,
            Vector3D gatePosition, BoundingSphereD gate)
        {
            Log.Info("processing filetransfer:" + fileTransferInfo.CreateLogString());

            var playerId = Sync.Players.TryGetPlayerIdentity(new(fileTransferInfo.SteamUserId));

            // to prevent from hungry trash collector
            if (playerId is {})
                playerId.LastLogoutTime = DateTime.Now;

            var gridBlueprints = file.Grids;
            if (gridBlueprints == null || gridBlueprints.Count < 1)
            {
                Log.Error("can't find any blueprints in: " + fileTransferInfo.CreateLogString());
                return;
            }

            var identitiesToChange = new Dictionary<long, long>();
            foreach (var (identityId, clientId) in file.PlayerIdsMap.Where(static b => !Sync.Players.TryGetPlayerId(b.Key, out _)))
            {
                var ob = file.IdentitiesMap[identityId];
                ob.IdentityId = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.IDENTITY);
                Sync.Players.CreateNewIdentity(ob);
                Sync.Players.InitNewPlayer(new(clientId), new()
                {
                    IdentityId = ob.IdentityId
                });
                identitiesToChange[identityId] = ob.IdentityId;
            }
            
            foreach (var cubeBlock in gridBlueprints.SelectMany(static b => b.CubeBlocks))
            {
                if (identitiesToChange.TryGetValue(cubeBlock.BuiltBy, out var builtBy))
                    cubeBlock.BuiltBy = builtBy;
                
                if (identitiesToChange.TryGetValue(cubeBlock.Owner, out var owner))
                    cubeBlock.Owner = owner;
            }

            var pos = Utilities.FindFreePos(gate, Utilities.FindGridsRadius(gridBlueprints));
            if (pos == null || !Utilities.UpdateGridsPositionAndStop(gridBlueprints, pos.Value))
            {
                Log.Warn("no free space available for grid '" + fileTransferInfo.GridName + "' at wormhole '" +
                         fileTransferInfo.DestinationWormhole + "'");
                return;
            }

            foreach (var cockpit in gridBlueprints.SelectMany(static grid => grid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>()))
            {
                if (cockpit.Pilot == null || !Config.PlayerRespawn)
                {
                    cockpit.Pilot = null;
                    continue;
                }

                if (cockpit.Pilot.OwningPlayerIdentityId == null)
                {
                    Log.Info("cannot find player, removing character from cockpit");
                    cockpit.Pilot = null;
                    continue;
                }

                var identity = Sync.Players.TryGetIdentity(cockpit.Pilot.OwningPlayerIdentityId.Value);

                if (identity.Character != null)
                {
                    // if there is a character, kill it and force reconnection
                    if (!string.IsNullOrEmpty(Config.ThisIp) && Sync.Players.GetOnlinePlayers()
                        .FirstOrDefault(b => b.Identity.IdentityId == identity.IdentityId) is { } player)
                    {
                        ModCommunication.SendMessageTo(new JoinServerMessage(Config.ThisIp), player.Id.SteamId);
                        MyMultiplayer.Static.DisconnectClient(player.Id.SteamId);
                    }
                    KillCharacter(identity.Character);
                }
                identity.PerformFirstSpawn();
                cockpit.Pilot.OwningPlayerIdentityId = identity.IdentityId;
            }
            MyEntities.RemapObjectBuilderCollection(gridBlueprints);
            foreach (var gridBlueprint in gridBlueprints)
                MyEntities.CreateFromObjectBuilderParallel(gridBlueprint, true);

            MyVisualScriptLogicProvider.CreateLightning(gatePosition);
        }

        public static void KillCharacter(MyCharacter character)
        {
            Log.Info("killing character " + character.DisplayName);
            if (character.IsUsing is MyCockpit cockpit)
                cockpit.RemovePilot();
            character.GetIdentity()?.ChangeCharacter(null);
            
            character.EnableBag(false);
            character.StatComp.DoDamage(character.StatComp.Health.MaxValue);
            character.Close();
        }

        private void EnsureDirectoriesCreated()
        {
            _gridDir ??= Path.Combine(Config.Folder, AdminGatesFolder);
            _gridDirSent ??= Path.Combine(Config.Folder, AdminGatesConfirmSentFolder);
            _gridDirReceived ??= Path.Combine(Config.Folder, AdminGatesConfirmReceivedFolder);
            if (!Directory.Exists(_gridDir))
                Directory.CreateDirectory(_gridDir);
            if (!Directory.Exists(_gridDirSent))
                Directory.CreateDirectory(_gridDirSent);
            if (!Directory.Exists(_gridDirReceived))
                Directory.CreateDirectory(_gridDirReceived);
        }
    }
}