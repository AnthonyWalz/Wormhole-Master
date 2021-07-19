using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Utils;
using VRage;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Wormhole
{
    [Category("wormhole")]
    public class WormholeCommands : CommandModule
    {
        public WormholePlugin Plugin => (WormholePlugin) Context.Plugin;

        [Command("show", "shows wormholes")]
        [Permission(MyPromoteLevel.None)]
        public void ShowWormholes()
        {
            foreach (var wormholeGate in Plugin.Config.WormholeGates)
            {
                var description = wormholeGate.Description;
                var color = "#ff00f7";
                if (description == null)
                    description = "";
                else
                    description = wormholeGate.Description.Replace("\\n", "\n");
                if (wormholeGate.HexColor != null && wormholeGate.HexColor != "") color = wormholeGate.HexColor;
                try
                {
                    MyVisualScriptLogicProvider.AddGPS(wormholeGate.Name, description,
                        new(wormholeGate.X, wormholeGate.Y, wormholeGate.Z), ColorUtils.TranslateColor(color), 0,
                        Context.Player.IdentityId);
                    Context.Respond("GPS added to your list if it didn't already exist");
                }
                catch
                {
                    Context.Respond(
                        "Error: tell an admin to check the Hexcolor setting make sure it has # at the begining");
                }
            }
        }

        [Command("showeveryone", "shows wormholes")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowEveryoneWormholes()
        {
            foreach (var wormholeGate in Plugin.Config.WormholeGates)
            {
                var description = wormholeGate.Description;
                var color = "#ff00f7";
                if (description == null)
                    description = "";
                else
                    description = wormholeGate.Description.Replace("\\n", "\n");
                if (wormholeGate.HexColor != null && wormholeGate.HexColor != "") color = wormholeGate.HexColor;
                try
                {
                    MyVisualScriptLogicProvider.AddGPSForAll(wormholeGate.Name, description,
                        new(wormholeGate.X, wormholeGate.Y, wormholeGate.Z), ColorUtils.TranslateColor(color));
                    Context.Respond("GPS added to everyone's list if it didn't already exist");
                }
                catch
                {
                    Context.Respond(
                        "Error: tell an admin to check the Hexcolor setting make sure it has # at the begining");
                }
            }
        }

        [Command("safezone", "adds safe zones")]
        [Permission(MyPromoteLevel.Admin)]
        public void Safezone(float radius = -1)
        {
            if (radius == -1) radius = (float) Plugin.Config.RadiusGate;
            var entities = MyEntities.GetEntities();
            if (entities != null)
                foreach (var entity in entities)
                {
                    if (entity == null || !(entity is MySafeZone)) continue;
                    if (entity.DisplayName.Contains("[NPC-IGNORE]_[Wormhole-SafeZone]")) entity.Close();
                }

            foreach (var server in Plugin.Config.WormholeGates)
            {
                var ob = new MyObjectBuilder_SafeZone
                {
                    PositionAndOrientation =
                        new MyPositionAndOrientation(new(server.X, server.Y, server.Z), Vector3.Forward, Vector3.Up),
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

            Context.Respond(
                "Deleted all entities with '[NPC-IGNORE]_[Wormhole-SafeZone]' in them and readded Safezones to each Wormhole");
        }

        [Command("addgates",
            "Adds gates to each wormhole (type: 1 = regular, 2 = rotating, 3 = advanced rotating, 4 = regular 53blocks, 5 = rotating 53blocks, 6 = advanced rotating 53blocks) (selfowned: true = owned by you) (ownerid = id of who you want it owned by)")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddGates(int type = 1, bool selfowned = false, long ownerid = 0L)
        {
            try
            {
                var entities = MyEntities.GetEntities();
                if (entities != null)
                    entities.Where(b => b != null)
                        .OfType<MyCubeGrid>()
                        .Where(b => b.DisplayName.Contains("[NPC-IGNORE]_[Wormhole-Gate]"))
                        .ForEach(b => b.Close());

                foreach (var server in Plugin.Config.WormholeGates)
                {
                    string prefab;
                    switch (type)
                    {
                        case 2:
                            prefab = "WORMHOLE_ROTATING";
                            break;
                        case 3:
                            prefab = "WORMHOLE_ROTATING_ADVANCED";
                            break;
                        case 4:
                            prefab = "WORMHOLE_53_BLOCKS";
                            break;
                        case 5:
                            prefab = "WORMHOLE_ROTATING_53_BLOCKS";
                            break;
                        case 6:
                            prefab = "WORMHOLE_ROTATING_ADVANCED_53_BLOCKS";
                            break;
                        default:
                            prefab = "WORMHOLE";
                            break;
                    }

                    var grids = MyPrefabManager.Static.GetGridPrefab(prefab);
                    var objectBuilderList = new List<MyObjectBuilder_EntityBase>();

                    foreach (var grid in grids)
                    {
                        foreach (var cubeBlock in grid.CubeBlocks)
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

                        objectBuilderList.Add(grid);
                    }

                    var firstGrid = true;
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

                Context.Respond(
                    "Deleted all entities with '[NPC-IGNORE]_[Wormhole-Gate]' in them and readded Gates to each Wormhole");
            }
            catch
            {
                Context.Respond("Error: make sure you add the mod for the gates you are using");
            }
        }

        [Command("thorshammer", "Summon the power of Mjölnir")]
        [Permission(MyPromoteLevel.Admin)]
        public void ThorsHammer()
        {
            MyVisualScriptLogicProvider.CreateLightning(Context.Player.GetPosition());
        }

        [Command("Clearsync", "try to remove all files in sync folder")]
        [Permission(MyPromoteLevel.Admin)]
        public void Clearsync()
        {
            var gridDir = new DirectoryInfo(Plugin.Config.Folder + "/" + WormholePlugin.AdminGatesFolder);

            if (!gridDir.Exists) return;

            foreach (var file in gridDir.GetFiles())
            {
                if (file == null) continue;

                try
                {
                    file.Delete();
                }
                catch
                {
                }
            }
        }

        [Command("clear characters", "try to remove all your character (if it duplicated or bugged)")]
        public void RemoveCharacters()
        {
            if (Context.Player == null)
                return;
            var identity = (MyIdentity) Context.Player.Identity;
            foreach (var character in MyEntities.GetEntities().OfType<MyCharacter>().Where(b =>
                b.GetPlayerIdentityId() == identity.IdentityId))
            {
                WormholePlugin.KillCharacter(character);
            }
            Sync.Players.KillPlayer((MyPlayer) Context.Player);
        }
    }
}