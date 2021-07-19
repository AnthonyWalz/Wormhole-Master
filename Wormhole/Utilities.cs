using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRageMath;

namespace Wormhole
{
    internal class Utilities
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool UpdateGridsPositionAndStop(IEnumerable<MyObjectBuilder_CubeGrid> grids, Vector3D newPosition)
        {
            return grids.All(grid =>
            {
                if (grid.PositionAndOrientation == null)
                {
                    Log.Warn("Position and Orientation Information missing from Grid in file.");
                    return false;
                }

                var gridPositionOrientation = grid.PositionAndOrientation.Value;
                gridPositionOrientation.Position = newPosition;
                grid.PositionAndOrientation = gridPositionOrientation;

                // reset velocity
                grid.AngularVelocity = new();
                grid.LinearVelocity = new();
                return true;
            });
        }

        public static bool UpdateGridsPositionAndStopLive(IMyEntity grids, Vector3D newPosition)
        {
            grids.PositionComp.SetPosition(newPosition);
            grids.Physics.LinearVelocity = new(0, 0, 0);
            grids.Physics.AngularVelocity = new(0, 0, 0);
            return true;
        }

        public static bool WormholeGateConfigUpdate()
        {
            var dir = new DirectoryInfo(WormholePlugin.Instance.Config.Folder);
            foreach (var wormhole in WormholePlugin.Instance.Config.WormholeGates)
            {
                dir.CreateSubdirectory("./" + WormholePlugin.AdminGatesConfig + "/" + wormhole.Name);
                var configDir = new DirectoryInfo(WormholePlugin.Instance.Config.Folder + "/" +
                                                  WormholePlugin.AdminGatesConfig + "/" + wormhole.Name);
                configDir.GetFiles().ForEach(b => b.Delete());
                File.Create(WormholePlugin.Instance.Config.Folder + "/" + WormholePlugin.AdminGatesConfig +
                            "/" + wormhole.Name + "/" + WormholePlugin.Instance.Config.ThisIp);
            }

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
                result = player.GetRelationTo(GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.FactionShare;
            return result;
        }

        public static long GetOwner(MyCubeGrid grid)
        {
            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                return gridOwnerList[0];
            if (ownerCnt > 1)
                return gridOwnerList[1];

            return gridOwner;
        }

        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character,
            bool includeConnectedGrids)
        {
            var grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new();

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
                foreach (var node in group.Nodes)
                {
                    var grid = node.NodeData;

                    if (grid.Physics == null)
                        continue;

                    grids.Add(grid);
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
                foreach (var node in group.Nodes)
                {
                    var grid = node.NodeData;

                    if (grid.Physics == null)
                        continue;

                    grids.Add(grid);
                }
            }

            return grids;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(
            string gridName)
        {
            var groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
            {
                foreach (var groupNodes in group.Nodes)
                {
                    var grid = groupNodes.NodeData;

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

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>
            FindLookAtGridGroupMechanical(IMyCharacter controlledEntity)
        {
            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix =
                controlledEntity
                    .GetHeadMatrix(
                        true); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups)
            foreach (var groupNodes in group.Nodes)
            {
                IMyCubeGrid cubeGrid = groupNodes.NodeData;

                if (cubeGrid != null)
                {
                    if (cubeGrid.Physics == null)
                        continue;

                    // check if the ray comes anywhere near the Grid before continuing.    
                    if (ray.Intersects(cubeGrid.WorldAABB).HasValue)
                    {
                        var hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                        if (hit.HasValue)
                        {
                            var distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                            if (list.TryGetValue(group, out var oldDistance))
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
                foreach (var groupNodes in group.Nodes)
                {
                    var grid = groupNodes.NodeData;

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

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindLookAtGridGroup(
            IMyCharacter controlledEntity)
        {
            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix =
                controlledEntity
                    .GetHeadMatrix(
                        true); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Physical.Groups)
            foreach (var groupNodes in group.Nodes)
            {
                IMyCubeGrid cubeGrid = groupNodes.NodeData;

                if (cubeGrid == null)
                    continue;

                if (cubeGrid.Physics == null)
                    continue;

                // check if the ray comes anywhere near the Grid before continuing.    
                if (!ray.Intersects(cubeGrid.WorldAABB).HasValue)
                    continue;

                var hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                if (!hit.HasValue)
                    continue;

                var distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                if (list.TryGetValue(group, out var oldDistance))
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

            var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();

            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }

        public static Vector3D? FindFreePos(BoundingSphereD gate, float sphereradius)
        {
            var rand = new Random();

            MyEntity safezone = null;
            var entities = MyEntities.GetEntitiesInSphere(ref gate);
            foreach (var myentity in entities)
                if (myentity is MySafeZone)
                    safezone = myentity;
            return MyEntities.FindFreePlaceCustom(
                gate.RandomToUniformPointInSphere(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()),
                sphereradius, 20, 5, 1, 0, safezone);
        }

        public static string LegalCharOnly(string text)
        {
            var legallist = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
            var temp = "";
            foreach (var character in text)
                if (legallist.IndexOf(character) != -1)
                    temp += character;
            return temp;
        }

        public static float FindGridsRadius(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            Vector3? vector = null;
            var gridradius = 0F;
            foreach (var mygrid in grids)
            {
                var gridSphere = mygrid.CalculateBoundingSphere();
                if (vector == null)
                {
                    vector = gridSphere.Center;
                    gridradius = gridSphere.Radius;
                    continue;
                }

                var distance = Vector3.Distance(vector.Value, gridSphere.Center);
                var newRadius = distance + gridSphere.Radius;
                if (newRadius > gridradius)
                    gridradius = newRadius;
            }

            return (float) new BoundingSphereD(vector.Value, gridradius).Radius;
        }

        // parsing helper
        public class TransferFileInfo
        {
            public string DestinationWormhole;
            public string GridName;
            public string PlayerName;
            public ulong SteamUserId;
            public DateTime Time;

            public static TransferFileInfo ParseFileName(string path)
            {
                TransferFileInfo info = new();
                var pathItems = path.Split('_');
                if (pathItems.Length != 10) return null;

                info.DestinationWormhole = pathItems[0];
                info.SteamUserId = ulong.Parse(pathItems[1]);
                info.PlayerName = pathItems[2];
                info.GridName = pathItems[3];

                var year = int.Parse(pathItems[4]);
                var month = int.Parse(pathItems[5]);
                var day = int.Parse(pathItems[6]);
                var hour = int.Parse(pathItems[7]);
                var minute = int.Parse(pathItems[8]);

                var lastPart = pathItems[9];
                if (lastPart.EndsWith(".sbc")) lastPart = lastPart.Substring(0, lastPart.Length - 4);
                var second = int.Parse(lastPart);


                info.Time = new(year, month, day, hour, minute, second);

                return info;
            }

            public string CreateLogString()
            {
                return
                    $"dest: {DestinationWormhole};steamid: {SteamUserId};playername: {PlayerName};gridName: {GridName};time:{Time:yyyy_MM_dd_HH_mm_ss};";
            }

            public string CreateFileName()
            {
                return
                    $"{DestinationWormhole}_{SteamUserId}_{LegalCharOnly(PlayerName)}_{LegalCharOnly(GridName)}_{Time:yyyy_MM_dd_HH_mm_ss}";
            }
        }
    }
}