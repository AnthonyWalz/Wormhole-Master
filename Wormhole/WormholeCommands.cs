using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using Torch;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using Wormhole.GridExport;
using Wormhole.Utils;
using VRageMath;
using Sandbox.Game.Entities.Cube;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

namespace Wormhole
{
    [Category("wormhole")]
    public class WormholeCommands : CommandModule{
        public WormholePlugin Plugin => (WormholePlugin) Context.Plugin;

        [Command("addgates", "Adds gates to each wormhole make sure they are clear before use")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddGates(string gridName = null)
        {
            MyCharacter character = null;
            if (gridName == null)
            {

                if (Context.Player == null)
                {
                    Context.Respond("You need to enter a Grid name of the grid.");
                    return;
                }

                var player = ((MyPlayer)Context.Player).Identity;

                if (player.Character == null)
                {
                    Context.Respond("Player has no character!");
                    return;
                }

                character = player.Character;
            }

            List<MyCubeGrid> grids = GridFinder.FindGridList(gridName, character, true);

            if (grids == null)
            {
                Context.Respond("Multiple grids found. Try to rename them first");
                return;
            }

            if (grids.Count == 0)
            {
                Context.Respond("No grids found. Check your viewing angle or try the correct name!");
                return;
            }

            try
            {
                foreach (Server server in Plugin.Config.WormholeServer)
                {
                    //spawngates(server.X, server.Y, server.Z);

                    List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();
                    foreach (MyCubeGrid grid in grids)
                    {
                        //grid.WorldMatrix.Translation.X = server.X;
                        if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                            throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");
                        objectBuilders.Add(objectBuilder);
                        if (Plugin.Config.BeaconOverwrite)
                        {
                            Context.Respond("IFBEACON");
                            var myblocks = objectBuilder.CubeBlocks;
                            foreach (var block in myblocks)
                            {

                                if (block is MyObjectBuilder_Beacon beacon)
                                { 
                                    Context.Respond("HUDTEXT");
                                    beacon.HudText = server.Name;
                                }
                            }
                        }
                    }

                    bool firstGrid = true;
                    double deltaX = 0;
                    double deltaY = 0;
                    double deltaZ = 0;

                    foreach (var grid in objectBuilders.ToArray())
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

                    List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>(objectBuilders);
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
                }
                foreach (MyCubeGrid grid in grids)
                {
                    grid.Delete();
                }
                Context.Respond("Done");
                return;
            }
            catch (Exception e)
            {}
        }
    }
}
