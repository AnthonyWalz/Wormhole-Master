using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using System;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
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
    }
}