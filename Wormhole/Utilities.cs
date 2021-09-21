using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRage.Groups;
using VRage.Library.Utils;
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
            var delta = biggestGrid.PositionAndOrientation!.Value.Position;

            return grids.All(grid =>
            {
                if (grid.PositionAndOrientation == null)
                {
                    Log.Warn($"Position and Orientation Information missing from Grid {grid.DisplayName} in file.");
                    return false;
                }

                var gridPositionOrientation = grid.PositionAndOrientation.Value;
                if (grid == biggestGrid)
                {
                    gridPositionOrientation.Position = newPosition;
                }
                else
                {
                    gridPositionOrientation.Position = newPosition + gridPositionOrientation.Position - delta;
                }
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

        public static bool TryParseGps(string raw, out string name, out Vector3D position, out Color color)
        {
            name = default;
            position = default;
            color = default;
            
            var parts = raw.Split(':');
            if (parts.Length != 7)
                return false;
            name = parts[1];
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var xCord) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var yCord) ||
                !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var zCord))
                return false;
            position = new (xCord, yCord, zCord);
            color = ColorUtils.TranslateColor(parts[5]);
            return true;
        }

        public static void SendConnectToServer(string address, ulong clientId)
        {
            ModCommunication.SendMessageTo(
                new JoinServerMessage(ToIpEndpoint(address, MySandboxGame.ConfigDedicated.ServerPort).ToString()),
                clientId);
        }
        
        public static IPEndPoint ToIpEndpoint(string hostNameOrAddress, int defaultPort)
        {
            var parts = hostNameOrAddress.Split(':');
            
            if (parts.Length == 2)
                defaultPort = int.Parse(parts[1]);
            
            var addrs = Dns.GetHostAddresses(parts[0]);
            return new (addrs.FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork)
                        ??
                        addrs.First(), defaultPort);
        }

        public static Vector3D PickRandomPointInSpheres(Vector3D center, float innerRadius, float outerRadius)
        {
            return center;
            var innerSphere = new BoundingSphereD(center, innerRadius);
            var outerSphere = new BoundingSphereD(center, outerRadius);

            for (var i = 0; i < 15; i++)
            {
                var pointInner = GetRandomPoint(innerSphere);
                var pointOuter = GetRandomPoint(outerSphere);
            
                var n = MyRandom.Instance.NextDouble();

                // idk how compute random point between 2 spheres, fucking math
                // TODO add math
            }
        }
        
        private static Vector3D GetRandomPoint(BoundingSphereD sphere)
        {
            var u = MyRandom.Instance.NextDouble();
            var v = MyRandom.Instance.NextDouble();
            var theta = 2 * Math.PI * u;
            var phi = Math.Acos(2 * v - 1);
            var x = sphere.Center.X + sphere.Radius * Math.Sin(phi) * Math.Cos(theta);
            var y = sphere.Center.Y + sphere.Radius * Math.Sin(phi) * Math.Sin(theta);
            var z = sphere.Center.Z + sphere.Radius * Math.Cos(phi);
            return new(x, y, z);
        }

        // parsing helper
        public class TransferFileInfo
        {
            public string DestinationWormhole;
            public string GridName;
            public string PlayerName;
            public ulong SteamUserId;

            public static TransferFileInfo ParseFileName(string path)
            {
                TransferFileInfo info = new ();
                var pathItems = path.Split('_');
                if (pathItems.Length != 4) return null;

                info.DestinationWormhole = pathItems[0];
                info.SteamUserId = ulong.Parse(pathItems[1]);
                info.PlayerName = pathItems[2];

                var lastPart = pathItems[3];
                if (lastPart.EndsWith(".sbcB5")) lastPart = lastPart.Substring(0, lastPart.Length - ".sbcB5".Length);
                info.GridName = lastPart;

                return info;
            }

            public string CreateLogString()
            {
                return
                    $"dest: {DestinationWormhole};steamid: {SteamUserId};playername: {PlayerName};gridName: {GridName};";
            }

            public string CreateFileName()
            {
                return
                    $"{DestinationWormhole}_{SteamUserId}_{LegalCharOnly(PlayerName)}_{LegalCharOnly(GridName)}";
            }
        }
    }
}