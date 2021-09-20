using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using NLog;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using Wormhole.Managers;
using Wormhole.ViewModels;
using Wormhole.Views;

namespace Wormhole
{
    public class Plugin : TorchPluginBase, IWpfPlugin
    {
        public static readonly Logger Log = LogManager.GetLogger("Wormhole");

        private readonly ConcurrentDictionary<ulong, (Utilities.TransferFileInfo, TransferFile, Vector3D,
                BoundingSphereD)>
            _queuedSpawns = new ();

        private Persistent<Config> _config;

        private Gui _control;

        // public const string AdminGatesConfig = "admingatesconfig";

        // private const string AdminGatesConfirmReceivedFolder = "admingatesconfirmreceived";
        // private const string AdminGatesConfirmSentFolder = "admingatesconfirmsent";
        private const string AdminGatesBackupFolder = "grids_backup";

        public const string AdminGatesFolder = "admingates";
        private Task _saveOnEnterTask;

        // The actual task of saving the game on exit or enter
        private Task _saveOnExitTask;

        private int _tick;

        private string _gridDir;

        // private string _gridDirSent;
        // private string _gridDirReceived;
        private string _gridDirBackup;
        
        private ClientEffectsManager _clientEffectsManager;
        private JumpManager _jumpManager;
        private DestinationManager _destinationManager;
        private WormholeDiscoveryManager _discoveryManager;
        private ServerQueryManager _serverQueryManager;

        public static Plugin Instance { get; private set; }
        public Config Config => _config?.Data;

        public UserControl GetControl() => _control ??= new (this);

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            SetupConfig();

            _clientEffectsManager = new (Torch);
            Torch.Managers.AddManager(_clientEffectsManager);
            _jumpManager = new (Torch);
            Torch.Managers.AddManager(_jumpManager);
            _destinationManager = new (Torch);
            Torch.Managers.AddManager(_destinationManager);
            _discoveryManager = new (Torch);
            Torch.Managers.AddManager(_discoveryManager);
            _serverQueryManager = new (Torch);
            Torch.Managers.AddManager(_serverQueryManager);

            Torch.GameStateChanged += (_, state) =>
            {
                if (state == TorchGameState.Loaded)
                    MyMultiplayer.Static.ClientJoined += OnClientConnected;
            };
        }

        #region WorkSources

        private void OnClientConnected(ulong clientId, string name)
        {
            if (!_queuedSpawns.TryRemove(clientId, out var tuple))
                return;

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
                    var gate = new BoundingSphereD(wormhole.Position, Config.GateRadius);
                    WormholeTransferOut(wormhole, gate);
                    WormholeTransferIn(wormhole.Name.Trim(), wormhole.Position, gate);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not run Wormhole");
            }
        }

        #endregion

        #region Config

        public void Save()
        {
            _config.Save();
            _clientEffectsManager.RecalculateVisualData();
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

        #endregion

        #region Outgoing Transferring

        public void WormholeTransferOut(GateViewModel gateViewModel, BoundingSphereD gate)
        {
            foreach (var grid in MyEntities.GetTopMostEntitiesInSphere(ref gate).OfType<MyCubeGrid>())
            {
                var gts = grid.GridSystems.TerminalSystem;
                if (gts == null)
                    continue;

                var jumpDrives = gts.Blocks.OfType<MyJumpDrive>().Where(_destinationManager.IsValidJd).ToList();

                foreach (var jumpDrive in jumpDrives)
                    WormholeTransferOutFile(grid, jumpDrive, gateViewModel, jumpDrives);
            }
        }

        private void WormholeTransferOutFile(MyCubeGrid grid, MyJumpDrive wormholeDrive,
            GateViewModel gateViewModel, IEnumerable<MyJumpDrive> wormholeDrives)
        {
            DestinationViewModel pickedDestination;

            if (Config.AutoSend && gateViewModel.Destinations.Count == 1)
                pickedDestination = gateViewModel.Destinations[0];
            else
                pickedDestination = _destinationManager.TryGetDestination(wormholeDrive, gateViewModel);

            if (pickedDestination is null)
                return;

            var playerInCharge = Sync.Players.GetControllingPlayer(grid);

            if (playerInCharge?.Identity is null ||
                !wormholeDrive.CanJumpAndHasAccess(playerInCharge.Identity.IdentityId) ||
                !Utilities.HasRightToMove(playerInCharge, grid))
                return;
            
            foreach (var disablingWormholeDrive in wormholeDrives)
                if (_destinationManager.IsValidJd(disablingWormholeDrive))
                    disablingWormholeDrive.Enabled = false;

            var grids = Utilities.FindGridList(grid, Config.IncludeConnectedGrids);

            if (grids.Count == 0)
                return;

            Task.Run(async () =>
            {
                var jumpTask = _jumpManager.StartJump(gateViewModel, playerInCharge, wormholeDrive.CubeGrid);

                if (pickedDestination is GateDestinationViewModel destination &&
                    !_discoveryManager.IsLocalGate(destination.Name) &&
                    _discoveryManager.GetGateByName(destination.Name, out var address) is { })
                {
                    var result =
                        (await Task.WhenAll(jumpTask, _serverQueryManager.IsServerFull(address))).Aggregate(
                            static(a, b) => a && b);
                    
                    if (!result)
                    {
                        MyVisualScriptLogicProvider.SendChatMessage("Destination server is FULL!", "Wormhole",
                            playerInCharge.Identity.IdentityId, MyFontEnum.Red);
                        
                        MyVisualScriptLogicProvider.ShowNotification("Destination server is FULL!", 15000,
                            MyFontEnum.Red, playerInCharge.Identity.IdentityId);
                        return;
                    }
                }

                await Torch.InvokeAsync(() =>
                {
                    // This is here because it can be thread unsafe, so just call it in game thread
                    wormholeDrive.CurrentStoredPower = 0;
                    
                    if (pickedDestination is GateDestinationViewModel gateDestination)
                        ProcessGateJump(gateDestination, grid, grids, wormholeDrive, gateViewModel, playerInCharge);
                    else if (pickedDestination is InternalDestinationViewModel internalDestination)
                        ProcessInternalGpsJump(internalDestination, grid, grids, wormholeDrive, gateViewModel,
                            playerInCharge);
                });
                _clientEffectsManager.NotifyJumpStatusChanged(JumpStatus.Succeeded, gateViewModel, grid);
            });
        }

        #endregion

        #region Outgoing Processing

        private void ProcessInternalGpsJump(InternalDestinationViewModel dest, MyCubeGrid grid, IReadOnlyCollection<MyCubeGrid> grids,
            MyJumpDrive wormholeDrive, GateViewModel gateViewModel, MyPlayer playerInCharge)
        {
            var pos = dest.TryParsePosition() ??
                      throw new InvalidOperationException($"Invalid gps position {dest.Gps}");
            var point = Utilities.PickRandomPointInSpheres(pos, dest.InnerRadius, dest.OuterRadius);
            
            var box = grids.Select(static b => b.PositionComp.WorldAABB)
                .Aggregate(static(a, b) => a.Include(b));
            var toGate = new BoundingSphereD(point, Config.GateRadius);

            var freePos = Utilities.FindFreePos(toGate,
                (float) BoundingSphereD.CreateFromBoundingBox(box).Radius);

            if (freePos is null)
                return;

            _clientEffectsManager.NotifyJumpStatusChanged(JumpStatus.Perform, gateViewModel, grid, freePos);

            MyVisualScriptLogicProvider.CreateLightning(gateViewModel.Position);
            Utilities.UpdateGridPositionAndStopLive(wormholeDrive.CubeGrid, freePos.Value);
            MyVisualScriptLogicProvider.CreateLightning(point);
        }
        
        private void ProcessGateJump(GateDestinationViewModel dest, MyCubeGrid grid, IReadOnlyCollection<MyCubeGrid> grids,
            MyJumpDrive wormholeDrive, GateViewModel gateViewModel, MyPlayer playerInCharge)
        {
            var destGate = _discoveryManager.GetGateByName(dest.Name, out var ownerIp);
                    
            if (_discoveryManager.IsLocalGate(dest.Name))
            {
                var box = grids.Select(static b => b.PositionComp.WorldAABB)
                    .Aggregate(static(a, b) => a.Include(b));
                var toGatePoint = destGate.Position;
                var toGate = new BoundingSphereD(toGatePoint, Config.GateRadius);

                var freePos = Utilities.FindFreePos(toGate,
                    (float) BoundingSphereD.CreateFromBoundingBox(box).Radius);

                if (freePos is null)
                    return;

                _clientEffectsManager.NotifyJumpStatusChanged(JumpStatus.Perform, gateViewModel, grid, freePos);

                MyVisualScriptLogicProvider.CreateLightning(gateViewModel.Position);
                Utilities.UpdateGridPositionAndStopLive(wormholeDrive.CubeGrid, freePos.Value);
                MyVisualScriptLogicProvider.CreateLightning(toGatePoint);
            }
            else
            {

                var transferFileInfo = new Utilities.TransferFileInfo
                {
                    DestinationWormhole = dest.Name,
                    SteamUserId = playerInCharge.Id.SteamId,
                    PlayerName = playerInCharge.DisplayName,
                    GridName = grid.DisplayName
                };

                Log.Info("creating filetransfer:" + transferFileInfo.CreateLogString());
                var filename = transferFileInfo.CreateFileName();

                _clientEffectsManager.NotifyJumpStatusChanged(JumpStatus.Perform, gateViewModel, grid);

                MyVisualScriptLogicProvider.CreateLightning(gateViewModel.Position);

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
                    Utilities.SendConnectToServer(ownerIp, playerSteamId);
                }

                using (var stream =
                    File.Create(Utilities.CreateBlueprintPath(Path.Combine(Config.Folder, AdminGatesFolder),
                        filename)))
                using (var compressStream = new GZipStream(stream, CompressionMode.Compress))
                    Serializer.Serialize(compressStream, new TransferFile
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
                    .Where(b => b.Character is { })) Utilities.KillCharacter(identity.Character);
                foreach (var cubeGrid in grids)
                {
                    cubeGrid.Close();
                }

                // Saves the game if enabled in config.
                if (!Config.SaveOnExit) return;
                // (re)Starts the task if it has never been started o´r is done
                if (_saveOnExitTask is null || _saveOnExitTask.IsCompleted)
                    _saveOnExitTask = Torch.Save();
            }
        }

        #endregion

        #region Ingoing Transferring

        public void WormholeTransferIn(string wormholeName, Vector3D gatePoint, BoundingSphereD gate)
        {
            EnsureDirectoriesCreated();

            var changes = false;

            foreach (var file in Directory.EnumerateFiles(_gridDir, "*.sbc")
                    .Where(s => Path.GetFileNameWithoutExtension(s).Split('_')[0] == wormholeName))
                //if file not null if file exists if file is done being sent and if file hasnt been received before
            {
                var fileName = Path.GetFileName(file);
                if (!File.Exists(file)) continue;

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
                    using var decompressStream = new GZipStream(stream, CompressionMode.Decompress);

                    transferFile = Serializer.Deserialize<TransferFile>(decompressStream);
                    if (transferFile.Grids is null || transferFile.IdentitiesMap is null)
                        throw new InvalidOperationException("File is empty or invalid");
                }
                catch (Exception e)
                {
                    Log.Error(e, $"Corrupted file at {fileName}");
                    continue;
                }

                if (Sync.Players.TryGetPlayerIdentity(new (fileTransferInfo.SteamUserId))?.Character is { } character)
                    Utilities.KillCharacter(character);

                // if player online, process now, else queue util player join
                if (Sync.Players.TryGetPlayerBySteamId(fileTransferInfo.SteamUserId) is { })
                    WormholeTransferInQueue(transferFile, fileTransferInfo, gatePoint, gate);
                else
                    _queuedSpawns[fileTransferInfo.SteamUserId] = (fileTransferInfo, transferFile, gatePoint, gate);

                changes = true;
                var backupPath = Path.Combine(_gridDirBackup, fileName);
                if (!File.Exists(backupPath))
                    File.Copy(Path.Combine(_gridDir, fileName), backupPath);
                File.Delete(Path.Combine(_gridDir, fileName));
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

            if (Sync.Players.TryGetPlayerIdentity(new (fileTransferInfo.SteamUserId)) is { } playerId)
                // to prevent from hungry trash collector
                playerId.LastLogoutTime = DateTime.Now;

            var gridBlueprints = file.Grids;
            if (gridBlueprints is null || gridBlueprints.Count < 1)
            {
                Log.Error("can't find any blueprints in: " + fileTransferInfo.CreateLogString());
                return;
            }

            var identitiesToChange = new Dictionary<long, long>();
            foreach (var (identityId, clientId) in file.PlayerIdsMap.Where(static b =>
                Sync.Players.TryGetPlayerIdentity(new (b.Value)) is null))
            {
                var ob = file.IdentitiesMap[identityId];
                ob.IdentityId = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.IDENTITY);
                Sync.Players.CreateNewIdentity(ob).PerformFirstSpawn();

                var id = new MyPlayer.PlayerId(clientId);

                Sync.Players.GetPrivateField<ConcurrentDictionary<MyPlayer.PlayerId, long>>("m_playerIdentityIds")[id] =
                    ob.IdentityId;
                Sync.Players.GetPrivateField<Dictionary<long, MyPlayer.PlayerId>>("m_identityPlayerIds")
                    [ob.IdentityId] = id;

                identitiesToChange[identityId] = ob.IdentityId;
            }

            foreach (var (oldIdentityId, clientId) in file.PlayerIdsMap)
            {
                var identity = Sync.Players.TryGetPlayerIdentity(new (clientId));

                if (identity is { })
                    identitiesToChange[oldIdentityId] = identity.IdentityId;
                else if (!identitiesToChange.ContainsKey(oldIdentityId) && Config.KeepOwnership)
                    Log.Warn(
                        $"New Identity id for {clientId} ({oldIdentityId}) not found! This will cause player to loose ownership");

                if (identity?.Character is { })
                    Utilities.KillCharacter(identity.Character);
            }

            MyIdentity requesterIdentity = null!;
            if (!Config.KeepOwnership)
                requesterIdentity = Sync.Players.TryGetPlayerIdentity(new (fileTransferInfo.SteamUserId));

            foreach (var cubeBlock in gridBlueprints.SelectMany(static b => b.CubeBlocks))
            {
                if (!Config.KeepOwnership && requesterIdentity is { })
                {
                    cubeBlock.Owner = requesterIdentity.IdentityId;
                    cubeBlock.BuiltBy = requesterIdentity.IdentityId;
                    continue;
                }

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

            var savedCharacters = new Dictionary<long, MyObjectBuilder_Character>();
            foreach (var cockpit in gridBlueprints.SelectMany(static grid =>
                grid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>()))
            {
                if (cockpit.Pilot is null) continue;

                var pilot = cockpit.Pilot;
                Utilities.RemovePilot(cockpit);

                if (!Config.PlayerRespawn) continue;

                if (!identitiesToChange.TryGetValue(pilot.OwningPlayerIdentityId.GetValueOrDefault(),
                    out var newIdentityId))
                    continue;

                savedCharacters[newIdentityId] = pilot;
            }

            MyEntities.RemapObjectBuilderCollection(gridBlueprints);
            foreach (var gridBlueprint in gridBlueprints)
            {
                var entity = MyEntities.CreateFromObjectBuilderNoinit(gridBlueprint);
                MyEntities.InitEntity(gridBlueprint, ref entity);
                MyEntities.Add(entity);
                OnGridSpawned(entity, savedCharacters);
            }

            MyVisualScriptLogicProvider.CreateLightning(gatePosition);
        }

        #endregion

        #region Characters Receiving

        [ReflectedMethodInfo(typeof(MyVisualScriptLogicProvider), "CloseRespawnScreen")]
        private static readonly MethodInfo CloseRespawnScreenMethod = null!;

        private static readonly Lazy<Action> CloseRespawnScreenAction =
            new (static() => CloseRespawnScreenMethod.CreateDelegate<Action>());

        private static void OnGridSpawned(MyEntity entity, IDictionary<long, MyObjectBuilder_Character> savedCharacters)
        {
            var grid = (MyCubeGrid) entity;

            // prefer spawn character in cryos/beds
            foreach (var cockpit in grid.GetFatBlocks().OfType<MyCockpit>()
                .OrderBy(static b => b is MyCryoChamber ? 0 : 1))
            {
                if (savedCharacters.Count < 1) break;
                if (cockpit.Pilot is { }) continue;

                var (identity, ob) = savedCharacters.First();
                savedCharacters.Remove(identity);

                ob.OwningPlayerIdentityId = identity;
                ob.EntityId = default;

                var matrix = cockpit.WorldMatrix;
                matrix.Translation -= Vector3.Up - Vector3.Forward;
                ob.PositionAndOrientation = new (matrix);

                var characterEntity = MyEntities.CreateFromObjectBuilderNoinit(ob);
                var character = (MyCharacter) characterEntity;
                MyEntities.InitEntity(ob, ref characterEntity);
                MyEntities.Add(character);

                var myIdentity = Sync.Players.TryGetIdentity(identity);
                Utilities.KillCharacters(myIdentity.SavedCharacters);
                myIdentity.ChangeCharacter(character);

                cockpit.AttachPilot(character, false, false, true);

                if (!Sync.Players.TryGetPlayerId(identity, out var playerId) ||
                    !Sync.Players.TryGetPlayerById(playerId, out var player)) continue;

                character.SetPlayer(player);
                Sync.Players.SetControlledEntity(Sync.Players.TryGetSteamId(identity), cockpit);

                Sync.Players.RevivePlayer(player);
                MySession.SendVicinityInformation(cockpit.CubeGrid.EntityId, new (playerId.SteamId));
                MyMultiplayer.RaiseStaticEvent(static _ => CloseRespawnScreenAction.Value, new (player.Id.SteamId));
            }
        }

        #endregion

        private void EnsureDirectoriesCreated()
        {
            _gridDir ??= Path.Combine(Config.Folder, AdminGatesFolder);
            // _gridDirSent ??= Path.Combine(Config.Folder, AdminGatesConfirmSentFolder);
            // _gridDirReceived ??= Path.Combine(Config.Folder, AdminGatesConfirmReceivedFolder);
            _gridDirBackup ??= Path.Combine(Config.Folder, AdminGatesBackupFolder);
            if (!Directory.Exists(_gridDir))
                Directory.CreateDirectory(_gridDir);
            // if (!Directory.Exists(_gridDirSent))
            //     Directory.CreateDirectory(_gridDirSent);
            // if (!Directory.Exists(_gridDirReceived))
            //     Directory.CreateDirectory(_gridDirReceived);
            if (!Directory.Exists(_gridDirBackup))
                Directory.CreateDirectory(_gridDirBackup);
        }
    }
}