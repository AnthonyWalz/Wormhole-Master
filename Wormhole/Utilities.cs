using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRageMath;

namespace Wormhole
{
    class Utilities
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static bool UpdateGridsPositionAndStop(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {
            bool firstGrid = true;
            Vector3D delta_NewPos_Grid0Pos = new Vector3D(0, 0, 0);

            foreach (var grid in grids)
            {
                if (grid.PositionAndOrientation == null)
                {
                    Log.Warn("Position and Orientation Information missing from Grid in file.");
                    return false;
                }
                var gridPositionOrientation = grid.PositionAndOrientation.Value;
                if (firstGrid)
                {
                    delta_NewPos_Grid0Pos = newPosition - gridPositionOrientation.Position;
                    gridPositionOrientation.Position = newPosition;
                    firstGrid = false;
                }
                else
                {
                    gridPositionOrientation.Position += delta_NewPos_Grid0Pos;
                }
                grid.PositionAndOrientation = gridPositionOrientation;

                // reset velocity
                grid.AngularVelocity = new SerializableVector3();
                grid.LinearVelocity = new SerializableVector3();
            }
            return true;
        }
        public static bool UpdateGridsPositionAndStopLive(IMyEntity grids, Vector3D newPosition)
        {
            grids.PositionComp.SetPosition(newPosition);
            grids.Physics.LinearVelocity = new Vector3(0, 0, 0);
            grids.Physics.AngularVelocity = new Vector3(0, 0, 0);
            return true;
        }

        public static string CreateBlueprintPath(string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName + ".sbc");
        }
        public static bool HasRightToMove(IMyPlayer player, MyCubeGrid grid)
        {
            var result = player.GetRelationTo(GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.Owner;
            if (WormholePlugin.Instance.Config.AllowInFaction && !result)
            {
                result = player.GetRelationTo(GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.FactionShare;
            }
            return result;
        }
        public static long GetOwner(MyCubeGrid grid)
        {

            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                return gridOwnerList[0];
            else if (ownerCnt > 1)
                return gridOwnerList[1];

            return gridOwner;
        }
        public static MyIdentity GetIdentityBySteamId(ulong steamId)
        {
            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                if (MySession.Static.Players.TryGetSteamId(identity.IdentityId) == steamId)
                {
                    return identity;
                }
            }
            return null;
        }
        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character, bool includeConnectedGrids)
        {

            List<MyCubeGrid> grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new List<MyCubeGrid>();

            if (includeConnectedGrids)
            {

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = FindLookAtGridGroup(character);
                else
                    groups = FindGridGroup(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {

                        MyCubeGrid grid = node.NodeData;

                        if (grid.Physics == null)
                            continue;

                        grids.Add(grid);
                    }
                }

            }
            else
            {

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = FindLookAtGridGroupMechanical(character);
                else
                    groups = FindGridGroupMechanical(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {

                        MyCubeGrid grid = node.NodeData;

                        if (grid.Physics == null)
                            continue;

                        grids.Add(grid);
                    }
                }
            }

            return grids;
        }
        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(string gridName)
        {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
            {

                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }
        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindLookAtGridGroupMechanical(IMyCharacter controlledEntity)
        {

            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups)
            {

                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                {

                    IMyCubeGrid cubeGrid = groupNodes.NodeData;

                    if (cubeGrid != null)
                    {

                        if (cubeGrid.Physics == null)
                            continue;

                        // check if the ray comes anywhere near the Grid before continuing.    
                        if (ray.Intersects(cubeGrid.WorldAABB).HasValue)
                        {

                            Vector3I? hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                            if (hit.HasValue)
                            {

                                double distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                                if (list.TryGetValue(group, out double oldDistance))
                                {

                                    if (distance < oldDistance)
                                    {
                                        list.Remove(group);
                                        list.Add(group, distance);
                                    }

                                }
                                else
                                {

                                    list.Add(group, distance);
                                }
                            }
                        }
                    }
                }
            }

            var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();

            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }
        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName)
        {

            var groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
            {

                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }
        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindLookAtGridGroup(IMyCharacter controlledEntity)
        {

            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Physical.Groups)
            {
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {
                    IMyCubeGrid cubeGrid = groupNodes.NodeData;

                    if (cubeGrid == null)
                        continue;

                    if (cubeGrid.Physics == null)
                        continue;

                    // check if the ray comes anywhere near the Grid before continuing.    
                    if (!ray.Intersects(cubeGrid.WorldAABB).HasValue)
                        continue;

                    Vector3I? hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                    if (!hit.HasValue)
                        continue;

                    double distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                    if (list.TryGetValue(group, out double oldDistance))
                    {

                        if (distance < oldDistance)
                        {
                            list.Remove(group);
                            list.Add(group, distance);
                        }

                    }
                    else
                    {

                        list.Add(group, distance);
                    }
                }
            }

            var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();

            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }
        public static Vector3D FindFreePos(BoundingSphereD gate, float sphereradius)
        {
            Random rand = new Random();

            MyEntity safezone = null;
            var entities = MyEntities.GetEntitiesInSphere(ref gate);
            foreach (MyEntity myentity in entities)
            {
                if (myentity is MySafeZone)
                {
                    safezone = myentity;
                }
            }
            return (Vector3D)MyEntities.FindFreePlaceCustom(gate.RandomToUniformPointInSphere(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()), sphereradius, 20, 5, 1, 0, safezone);
        }
        public static string LegalCharOnly(string text)
        {
            string legallist = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
            string temp = "";
            foreach (char character in text)
            {
                if (legallist.IndexOf(character) != -1)
                {
                    temp += character;
                }
            }
            return temp;
        }
        public static float FindGridsRadius(MyObjectBuilder_CubeGrid[] grids)
        {
            Vector3? vector = null;
            float gridradius = 0F;
            foreach (MyObjectBuilder_CubeGrid mygrid in grids)
            {
                var gridSphere = mygrid.CalculateBoundingSphere();
                if (vector == null)
                {
                    vector = gridSphere.Center;
                    gridradius = gridSphere.Radius;
                    continue;
                }
                float distance = Vector3.Distance(vector.Value, gridSphere.Center);
                float newRadius = distance + gridSphere.Radius;
                if (newRadius > gridradius)
                    gridradius = newRadius;
            }
            return (float)(new BoundingSphereD(vector.Value, gridradius)).Radius;
        }

        // parsing helper
        public struct TransferFileInfo
        {
            public string destinationWormhole;
            public ulong steamUserId;
            public string playerName;
            public string gridName;
            public DateTime time;
            public static TransferFileInfo? parseFileName(string path)
            {
                TransferFileInfo info;
                var pathItems = path.Split('_');
                if (pathItems.Count() != 10)
                {
                    return null;
                }
                else
                {
                    info.destinationWormhole = pathItems[0];
                    info.steamUserId = ulong.Parse(pathItems[1]);
                    info.playerName = pathItems[2];
                    info.gridName = pathItems[3];

                    var year = int.Parse(pathItems[4]);
                    var month = int.Parse(pathItems[5]);
                    var day = int.Parse(pathItems[6]);
                    var hour = int.Parse(pathItems[7]);
                    var minute = int.Parse(pathItems[8]);
                    var second = int.Parse(pathItems[9]);

                    info.time = new DateTime(year, month, day, hour, minute, second);

                    return info;
                }
            }
            public string createFileName()
            {
                return $"{destinationWormhole}_{steamUserId}_{Utilities.LegalCharOnly(playerName)}_{Utilities.LegalCharOnly(gridName)}_{time:yyyy_MM_dd_HH_mm_ss}";
            }
        }
    }
}