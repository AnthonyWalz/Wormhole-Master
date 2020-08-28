using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System.Collections.Generic;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using Torch.Utils;
using VRage;
using Sandbox.Game;
using VRage.Game.Entity;

namespace Wormhole
{
    [Category("wormhole")]
    public class WormholeCommands : CommandModule{
        public WormholePlugin Plugin => (WormholePlugin) Context.Plugin;

        [Command("show", "shows wormholes")]
        [Permission(MyPromoteLevel.None)]
        public void ShowWormholes()
        {
            foreach (var WormholeGate in Plugin.Config.WormholeGates)
            {
                var description = WormholeGate.Description;
                var color = "#ff00f7";
                if (description == null) 
                { 
                    description = ""; 
                }
                else 
                {
                    description = WormholeGate.Description.Replace("\\n", "\n");
                }
                if (WormholeGate.HexColor != null && WormholeGate.HexColor != "")
                {
                    color = WormholeGate.HexColor;
                }
                try
                {
                    MyVisualScriptLogicProvider.AddGPS(WormholeGate.Name, description, new Vector3D(WormholeGate.X, WormholeGate.Y, WormholeGate.Z), ColorUtils.TranslateColor(color), 0, Context.Player.IdentityId);
                    Context.Respond("GPS added to your list if it didn't already exist");
                }
                catch
                {
                    Context.Respond("Error: tell an admin to check the Hexcolor setting make sure it has # at the begining");
                }
            }
        }
        [Command("showeveryone", "shows wormholes")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowEveryoneWormholes()
        {
            foreach (var WormholeGate in Plugin.Config.WormholeGates)
            {
                var description = WormholeGate.Description;
                var color = "#ff00f7";
                if (description == null)
                {
                    description = "";
                }
                else
                {
                    description = WormholeGate.Description.Replace("\\n", "\n");
                }
                if (WormholeGate.HexColor != null && WormholeGate.HexColor != "")
                {
                    color = WormholeGate.HexColor;
                }
                try
                {
                    MyVisualScriptLogicProvider.AddGPSForAll(WormholeGate.Name, description, new Vector3D(WormholeGate.X, WormholeGate.Y, WormholeGate.Z), ColorUtils.TranslateColor(color), 0);
                    Context.Respond("GPS added to everyone's list if it didn't already exist");
                }
                catch
                {
                    Context.Respond("Error: tell an admin to check the Hexcolor setting make sure it has # at the begining");
                }
            }
        }
        [Command("safezone", "adds safe zones")]
        [Permission(MyPromoteLevel.Admin)]
        public void Safezone(float radius = -1)
        {
            if (radius == -1) {
                radius = (float)Plugin.Config.RadiusGate;
            }
            var entities = MyEntities.GetEntities();
            if (entities != null)
            {
                foreach (MyEntity entity in entities)
                {
                    if (entity != null)
                    {
                        if (entity is MySafeZone)
                        {
                            if (entity.DisplayName.Contains("[NPC-IGNORE]_[Wormhole-SafeZone]"))
                            {
                                entity.Close();
                            }
                        }
                    }
                }
            }
            foreach (var server in Plugin.Config.WormholeGates)
            {
                var ob = new MyObjectBuilder_SafeZone
                {
                    PositionAndOrientation = new MyPositionAndOrientation(new Vector3D(server.X, server.Y, server.Z), Vector3.Forward, Vector3.Up),
                    PersistentFlags = MyPersistentEntityFlags2.InScene,
                    Shape = MySafeZoneShape.Sphere,
                    Radius = radius,
                    Enabled = true,
                    DisplayName = "[NPC-IGNORE]_[Wormhole-SafeZone]_" + server.Name,
                    AccessTypeGrids = MySafeZoneAccess.Blacklist,
                    AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist,
                    AccessTypeFactions = MySafeZoneAccess.Blacklist,
                    AccessTypePlayers = MySafeZoneAccess.Blacklist
                };
                MyEntities.CreateFromObjectBuilderAndAdd(ob, true);
            }
            Context.Respond("Deleted all entities with '[NPC-IGNORE]_[Wormhole-SafeZone]' in them and readded Safezones to each Wormhole");
        }
        [Command("addgates", "Adds gates to each wormhole (type: 1 = regular, 2 = rotating, 3 = advanced rotating, 4 = regular 53blocks, 5 = rotating 53blocks, 6 = advanced rotating 53blocks) (selfowned: true = owned by you) (ownerid = id of who you want it owned by)")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddGates(int type = 1, bool selfowned = false, long ownerid = 0L)
        {
            
            try
            {
                var entities = MyEntities.GetEntities();
                if (entities != null)
                {
                    foreach (MyEntity entity in entities)
                    {
                        if (entity != null)
                        {
                            if (entity is MyCubeGrid)
                            {
                                if (entity.DisplayName.Contains("[NPC-IGNORE]_[Wormhole-Gate]"))
                                {
                                    entity.Close();
                                }
                            }
                        }
                    }
                }
                foreach (var server in Plugin.Config.WormholeGates)
                {
                    string prefab;
                    if (type == 2)
                    {
                        prefab = "WORMHOLE_ROTATING";
                    }
                    else if (type == 3)
                    {
                        prefab = "WORMHOLE_ROTATING_ADVANCED";
                    }
                    else if (type == 4)
                    {
                        prefab = "WORMHOLE_53_BLOCKS";
                    }
                    else if (type == 5)
                    {
                        prefab = "WORMHOLE_ROTATING_53_BLOCKS";
                    }
                    else if (type == 6)
                    {
                        prefab = "WORMHOLE_ROTATING_ADVANCED_53_BLOCKS";
                    }
                    else
                    {
                        prefab = "WORMHOLE";
                    }
                    MyObjectBuilder_CubeGrid[] grids = MyPrefabManager.Static.GetGridPrefab(prefab);
                    List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>();
                    foreach (var grid in grids)
                    {
                        foreach (MyObjectBuilder_CubeBlock cubeBlock in grid.CubeBlocks)
                        {
                            if (selfowned)
                            {
                                cubeBlock.Owner = Context.Player.IdentityId;
                                cubeBlock.BuiltBy = Context.Player.IdentityId;
                            }
                            else
                            {
                                cubeBlock.Owner = ownerid;
                                cubeBlock.BuiltBy = ownerid;
                            }
                        }
                        objectBuilderList.Add(grid as MyObjectBuilder_EntityBase);
                    }

                    bool firstGrid = true;
                    double deltaX = 0;
                    double deltaY = 0;
                    double deltaZ = 0;

                    foreach (var grid in grids)
                    {
                        var position = grid.PositionAndOrientation;
                        var realPosition = position.Value;
                        var currentPosition = realPosition.Position;

                        if (firstGrid)
                        {
                            deltaX = server.X - currentPosition.X;
                            deltaY = server.Y - currentPosition.Y;
                            deltaZ = server.Z - currentPosition.Z;

                            currentPosition.X = server.X;
                            currentPosition.Y = server.Y;
                            currentPosition.Z = server.Z;

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
                    MyEntities.RemapObjectBuilderCollection(objectBuilderList);
                    MyEntities.Load(objectBuilderList, out _);
                }
                Context.Respond("Deleted all entities with '[NPC-IGNORE]_[Wormhole-Gate]' in them and readded Gates to each Wormhole");
            }
            catch {
                Context.Respond("Error: make sure you add the mod for the gates you are using");
            }
        }
        [Command("thorshammer", "Summon the power of Mjölnir")]
        [Permission(MyPromoteLevel.Admin)]
        public void ThorsHammer()
        {
            MyVisualScriptLogicProvider.CreateLightning(Context.Player.GetPosition());
        }
    }
}