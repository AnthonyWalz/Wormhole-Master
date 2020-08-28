using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRageMath;
using Wormhole.Utils;

namespace Wormhole.GridExport
{

    public class GridManager
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool SaveGrid(string path, string filename, string ip, bool keepOriginalOwner, bool keepProjection, List<MyCubeGrid> grids)
        {
            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids)
            {

                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            return SaveGrid(path, filename, ip, keepOriginalOwner, keepProjection, objectBuilders);
        }

        public static bool SaveGrid(string path, string filename, string ip, bool keepOriginalOwner, bool keepProjection, List<MyObjectBuilder_CubeGrid> objectBuilders)
        {

            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();
            List<ulong> playerIds = new List<ulong>();
            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
            {
                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                {

                    if (!keepOriginalOwner)
                    {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                    }

                    /* Remove Projections if not needed */
                    if (!keepProjection)
                        if (cubeBlock is MyObjectBuilder_ProjectorBase projector)
                            projector.ProjectedGrids = null;

                    /* Remove Pilot and Components (like Characters) from cockpits */
                    if (cubeBlock is MyObjectBuilder_Cockpit cockpit)
                    {
                        if (cockpit.Pilot != null)
                        {
                            var playersteam = cockpit.Pilot.PlayerSteamId;
                            var player = PlayerUtils.GetIdentityByNameOrId(playersteam.ToString());
                            playerIds.Add(playersteam);
                            ModCommunication.SendMessageTo(new JoinServerMessage(ip), playersteam);
                        }
                    }
                }
            }

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };
            foreach(var playerId in playerIds)
            {
                var player = PlayerUtils.GetIdentityByNameOrId(playerId.ToString());
                player.Character.EnableBag(false);
                Sandbox.Game.MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 0);
                player.Character.Delete();
            }
            return MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);
        }
        public static GridImportResult LoadGrid(string path, BoundingSphereD position, double cutout, bool keepOriginalLocation, long playerid, bool KeepOriginalOwner, bool PlayerRespawn, string ThisIp, bool force = false)
        {

            if (!File.Exists(path))
                return GridImportResult.FILE_NOT_FOUND;

            if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
            {

                var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;

                if (shipBlueprints == null)
                {
                    Log.Warn("No ShipBlueprints in File '" + path + "'");
                    return GridImportResult.NO_GRIDS_IN_FILE;
                }

                foreach (var shipBlueprint in shipBlueprints)
                {
                    GridImportResult result = LoadShipBlueprint(shipBlueprint, position, cutout, keepOriginalLocation, playerid, KeepOriginalOwner, PlayerRespawn, ThisIp, force);

                    if (result != GridImportResult.OK)
                    {
                        Log.Warn("Error Loading ShipBlueprints from File '" + path + "'");
                        return result;
                    }
                }
                return GridImportResult.OK;
            }

            Log.Warn("Error Loading File '" + path + "' check Keen Logs.");

            return GridImportResult.UNKNOWN_ERROR;
        }

        private static GridImportResult LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint, BoundingSphereD position, double cutout, bool keepOriginalLocation, long playerid, bool KeepOriginalOwner, bool PlayerRespawn, string ThisIp, bool force = false)
        {

            var grids = shipBlueprint.CubeGrids;

            if (grids == null || grids.Length == 0)
            {
                Log.Warn("No grids in blueprint!");
                return GridImportResult.NO_GRIDS_IN_BLUEPRINT;
            }
            List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>(grids.ToList());
            if (!keepOriginalLocation)
            {

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids, position, cutout);
                if (pos == null)
                {
                    Log.Warn("No free Space found!");
                    return GridImportResult.NO_FREE_SPACE_AVAILABLE;
                }

                var newPosition = pos.Value;

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newPosition))
                    return GridImportResult.NOT_COMPATIBLE;

            }
            else if (!force)
            {

                var sphere = FindBoundingSphere(grids);

                var gridsPosition = grids[0].PositionAndOrientation.Value;

                sphere.Center = gridsPosition.Position;

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);

                foreach (var entity in entities)
                    if (entity is MyCubeGrid)
                        return GridImportResult.POTENTIAL_BLOCKED_SPACE;
            }
            /* Stop grids */
            foreach (var grid in grids)
            {
                foreach (var block in grid.CubeBlocks)
                {
                    if (!KeepOriginalOwner)
                    {
                        block.BuiltBy = playerid;
                        block.Owner = playerid;
                    }
                    if (block is MyObjectBuilder_Cockpit cockpit)
                    {
                        if (cockpit.Pilot != null)
                        {
                            var player = PlayerUtils.GetIdentityByNameOrId(cockpit.Pilot.PlayerSteamId.ToString());
                            if (PlayerUtils.GetIdentityByNameOrId(cockpit.Pilot.PlayerSteamId.ToString()) != null && PlayerRespawn)
                            {
                                cockpit.Pilot.OwningPlayerIdentityId = player.IdentityId;
                                if (player.Character != null)
                                {
                                    if (ThisIp != null && ThisIp != "") {
                                        ModCommunication.SendMessageTo(new JoinServerMessage(ThisIp), player.Character.ControlSteamId);
                                    }
                                    player.Character.EnableBag(false);
                                    Sandbox.Game.MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 0);
                                    player.Character.Delete();
                                }
                                player.PerformFirstSpawn();
                                player.SavedCharacters.Clear();
                                player.SavedCharacters.Add(cockpit.Pilot.EntityId);
                                MyAPIGateway.Multiplayer.Players.SetControlledEntity(cockpit.Pilot.PlayerSteamId, cockpit.Pilot as VRage.ModAPI.IMyEntity);
                            }
                            else
                            {
                                cockpit.Pilot = null;
                            }
                        }
                    }
                }
                grid.AngularVelocity = new SerializableVector3();
                grid.LinearVelocity = new SerializableVector3();
            }
            MyEntities.RemapObjectBuilderCollection(objectBuilderList);

            bool hasMultipleGrids = objectBuilderList.Count > 1;

            if (!hasMultipleGrids)
            {

                foreach (var ob in objectBuilderList)
                    MyEntities.CreateFromObjectBuilderParallel(ob, true);

            }
            else
            {
                MyEntities.Load(objectBuilderList, out _);
            }
            return GridImportResult.OK;
        }
        private static bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            foreach (var grid in grids)
            {

                var position = grid.PositionAndOrientation;

                if (position == null)
                {

                    Log.Warn("Position and Orientation Information missing from Grid in file.");

                    return false;
                }

                var realPosition = position.Value;

                var currentPosition = realPosition.Position;

                if (firstGrid)
                {
                    deltaX = newPosition.X - currentPosition.X;
                    deltaY = newPosition.Y - currentPosition.Y;
                    deltaZ = newPosition.Z - currentPosition.Z;

                    currentPosition.X = newPosition.X;
                    currentPosition.Y = newPosition.Y;
                    currentPosition.Z = newPosition.Z;

                    firstGrid = false;

                }
                else
                {

                    currentPosition.X += deltaX;
                    currentPosition.Y += deltaY;
                    currentPosition.Z += deltaZ;
                }

                realPosition.Position = currentPosition;
                grid.PositionAndOrientation = realPosition;
            }

            return true;
        }
        private static Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, BoundingSphereD position, double cutout)
        {

            BoundingSphere sphere = FindBoundingSphere(grids);

            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */

            Random rand = new Random();
            MyEntity safezone = null;
            var entities = MyEntities.GetEntitiesInSphere(ref position);
            foreach(MyEntity entity in entities)
            {
                if(entity is MySafeZone)
                {
                    safezone = entity;
                }
            }
            return MyEntities.FindFreePlaceCustom(position.RandomToUniformPointInSphereWithInnerCutout(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), cutout).GetValueOrDefault(), sphere.Radius + 50, 20, 5, 1, 0, safezone);
        }

        private static BoundingSphereD FindBoundingSphere(MyObjectBuilder_CubeGrid[] grids)
        {

            Vector3? vector = null;
            float radius = 0F;

            foreach (var grid in grids)
            {

                var gridSphere = grid.CalculateBoundingSphere();

                /* If this is the first run, we use the center of that grid, and its radius as it is */
                if (vector == null)
                {

                    vector = gridSphere.Center;
                    radius = gridSphere.Radius;
                    continue;
                }

                /* 
                 * If its not the first run, we use the vector we already have and 
                 * figure out how far it is away from the center of the subgrids sphere. 
                 */
                float distance = Vector3.Distance(vector.Value, gridSphere.Center);

                /* 
                 * Now we figure out how big our new radius must be to house both grids
                 * so the distance between the center points + the radius of our subgrid.
                 */
                float newRadius = distance + gridSphere.Radius;

                /*
                 * If the new radius is bigger than our old one we use that, otherwise the subgrid 
                 * is contained in the other grid and therefore no need to make it bigger. 
                 */
                if (newRadius > radius)
                    radius = newRadius;
            }

            return new BoundingSphereD(vector.Value, radius);
        }
    }
}
