using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRage.Groups;
using VRage.ModAPI;
using VRageMath;

namespace Wormhole
{
    internal class Utilities
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool UpdateGridsPositionAndStop(ICollection<MyObjectBuilder_CubeGrid> grids, Vector3D newPosition)
        {
            var biggestGrid = grids.OrderByDescending(static b => b.CubeBlocks.Count).First();
            newPosition -= FindGridsBoundingSphere(grids, biggestGrid).Center -
                           biggestGrid.PositionAndOrientation!.Value.Position;
            var delta = newPosition - biggestGrid.PositionAndOrientation!.Value.Position;

            return grids.All(grid =>
            {
                if (grid.PositionAndOrientation == null)
                {
                    Log.Warn($"Position and Orientation Information missing from Grid {grid.DisplayName} in file.");
                    return false;
                }

                var gridPositionOrientation = grid.PositionAndOrientation.Value;
                gridPositionOrientation.Position = newPosition + delta;
                grid.PositionAndOrientation = gridPositionOrientation;

                // reset velocity
                grid.AngularVelocity = new ();
                grid.LinearVelocity = new ();
                return true;
            });
        }

        public static void UpdateGridPositionAndStopLive(MyCubeGrid grid, Vector3D newPosition)
        {
            var matrix = grid.PositionComp.WorldMatrixRef;
            matrix.Translation = newPosition;
            grid.Teleport(matrix);
            grid.Physics.LinearVelocity = Vector3.Zero;
            grid.Physics.AngularVelocity = Vector3.Zero;
        }

        public static string CreateBlueprintPath(string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, fileName + ".sbc");
        }

        public static bool HasRightToMove(IMyPlayer player, MyCubeGrid grid)
        {
            var result = player.GetRelationTo(GetOwner(grid)) == MyRelationsBetweenPlayerAndBlock.Owner;
            if (Plugin.Instance.Config.AllowInFaction && !result)
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

        public static List<MyCubeGrid> FindGridList(MyCubeGrid grid, bool includeConnectedGrids)
        {
            var list = new List<MyCubeGrid>();

            list.AddRange(includeConnectedGrids
                ? MyCubeGridGroups.Static.Physical.GetGroup(grid).Nodes.Select(static b => b.NodeData)
                : MyCubeGridGroups.Static.Mechanical.GetGroup(grid).Nodes.Select(static b => b.NodeData));

            return list;
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
            return string.Join(string.Empty, text.Where(static b => char.IsLetter(b) || char.IsNumber(b)));
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

        public static BoundingSphereD FindGridsBoundingSphere(IEnumerable<MyObjectBuilder_CubeGrid> grids,
            MyObjectBuilder_CubeGrid biggestGrid)
        {
            var boxD = BoundingBoxD.CreateInvalid();
            boxD.Include(biggestGrid.CalculateBoundingBox());
            var matrix = biggestGrid.PositionAndOrientation!.Value.GetMatrix();
            var matrix2 = MatrixD.Invert(matrix);
            var array = new Vector3D[8];
            foreach (var grid in grids)
            {
                if (grid == biggestGrid) continue;

                BoundingBoxD box = grid.CalculateBoundingBox();
                var myOrientedBoundingBoxD =
                    new MyOrientedBoundingBoxD(box, grid.PositionAndOrientation!.Value.GetMatrix());
                myOrientedBoundingBoxD.Transform(matrix2);
                myOrientedBoundingBoxD.GetCorners(array, 0);

                foreach (var point in array)
                {
                    boxD.Include(point);
                }
            }

            var boundingSphereD = BoundingSphereD.CreateFromBoundingBox(boxD);
            return new (new MyOrientedBoundingBoxD(boxD, matrix).Center, boundingSphereD.Radius);
        }

        public static void KillCharacter(MyCharacter character)
        {
            Log.Info("killing character " + character.DisplayName);
            if (character.IsUsing is MyCockpit cockpit)
                cockpit.RemovePilot();
            character.GetIdentity()?.ChangeCharacter(null);

            character.EnableBag(false);
            character.Close();
        }

        public static void KillCharacters(ICollection<long> characters)
        {
            foreach (var character in characters)
            {
                if (!MyEntities.TryGetEntityById<MyCharacter>(character, out var entity))
                    continue;
                KillCharacter(entity);
            }

            characters.Clear();
        }

        public static void RemovePilot(MyObjectBuilder_Cockpit cockpit)
        {
            // wasted 15 hours to find this fucking HierarchyComponent trap
            cockpit.Pilot = null;
            var component = cockpit.ComponentContainer.Components.FirstOrDefault(static b =>
                b.Component is MyObjectBuilder_HierarchyComponentBase);

            ((MyObjectBuilder_HierarchyComponentBase) component?.Component)?.Children.Clear();
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
                TransferFileInfo info = new ();
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


                info.Time = new (year, month, day, hour, minute, second);

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