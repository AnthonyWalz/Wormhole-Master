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

namespace Wormhole
{
    [Category("wormhole")]
    public class WormholeCommands : CommandModule{
        public WormholePlugin Plugin => (WormholePlugin) Context.Plugin;

        [Command("show", "shows wormholes")]
        [Permission(MyPromoteLevel.None)]
        public void ShowWormholes()
        {
            foreach (var server in Plugin.Config.WormholeServer)
            {
                Sandbox.Game.MyVisualScriptLogicProvider.AddGPS(server.Name, server.Description.Replace("\\n","\n"), new Vector3D(server.X, server.Y, server.Z), ColorUtils.TranslateColor(server.HexColor), 0, Context.Player.IdentityId);
            }
        }
        [Command("showeveryone", "shows wormholes")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowEveryoneWormholes()
        {
            foreach (var server in Plugin.Config.WormholeServer)
            {
                Sandbox.Game.MyVisualScriptLogicProvider.AddGPSForAll(server.Name, server.Description.Replace("\\n", "\n"), new Vector3D(server.X, server.Y, server.Z), ColorUtils.TranslateColor(server.HexColor), 0);
            }
        }
        [Command("safezone", "adds safe zones")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowEveryoneWormholes(float radius = -1)
        {
            if (radius == -1) {
                radius = (float)Plugin.Config.OutOutsideRadiusGate;
            }
            foreach (var server in Plugin.Config.WormholeServer)
            {
                var ob = new MyObjectBuilder_SafeZone();
                ob.PositionAndOrientation = new MyPositionAndOrientation(new Vector3D(server.X, server.Y, server.Z), Vector3.Forward, Vector3.Up);
                ob.PersistentFlags = MyPersistentEntityFlags2.InScene;
                ob.Shape = MySafeZoneShape.Sphere;
                ob.Radius = radius;
                ob.Enabled = true;
                ob.DisplayName = server.Name;
                ob.AccessTypeGrids = MySafeZoneAccess.Blacklist;
                ob.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;
                ob.AccessTypeFactions = MySafeZoneAccess.Blacklist;
                ob.AccessTypePlayers = MySafeZoneAccess.Blacklist;
                var zone = MyEntities.CreateFromObjectBuilderAndAdd(ob, true);
            }
        }
        [Command("addgates", "Adds gates to each wormhole (type: 1 = regular, 2 = rotating, 3 = advanced rotating, 4 = regular 53blocks, 5 = rotating 53blocks, 6 = advanced rotating 53blocks) (selfowned: true = owned by you) (ownerid = id of who you want it owned by)")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddGates(int type = 1, bool selfowned = false, long ownerid = 0L)
        {
            foreach (var server in Plugin.Config.WormholeServer)
            {
                string prefab = "";
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
        }
        [Command("thorshammer", "Summon the power of Mjölnir")]
        [Permission(MyPromoteLevel.Admin)]
        public void thorsHammer()
        {
            Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(Context.Player.GetPosition());
        }
    }
}
/*
    //System.Net.NetworkInformation.Ping
    //System.Net.Mail.
    //Sandbox.Game.MyVisualScriptLogicProvider.CreateLightning(new Vector3D(0, 0, 0));
    //Sandbox.Game.MyVisualScriptLogicProvider.CreateParticleEffectAtEntity("Warp", Context.Player.DisplayName);
    //Sandbox.Game.MyVisualScriptLogicProvider.CreateParticleEffectAtPosition("Warp", new Vector3D(0, 0, 0));
    //Sandbox.Game.MyVisualScriptLogicProvider.SetCustomLoadingScreenImage
    //Sandbox.Game.MyVisualScriptLogicProvider.PlaySingleSoundAtPosition
    //Sandbox.Game.MyVisualScriptLogicProvider.UnlockAchievementById
    //Sandbox.Game.MyVisualScriptLogicProvider.StartCutscene
    //Sandbox.Game.World.Generator.
    //Sandbox.Game.MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId,0);
                
 */
