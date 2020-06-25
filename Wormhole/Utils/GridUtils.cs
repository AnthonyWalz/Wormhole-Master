using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System.Collections.Generic;
using System.Linq;
using Torch.Commands;
using VRage.Game;
using VRage.Groups;

namespace Wormhole.Utils {

    public class GridUtils {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool Repair(MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group group, CommandContext Context) {

            foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes) {

                MyCubeGrid grid = groupNodes.NodeData;

                var gridOwner = OwnershipUtils.GetOwner(grid);

                HashSet<MySlimBlock> blocks = grid.GetBlocks();
                foreach (MySlimBlock block in blocks) {

                    long owner = block.OwnerId;
                    if (owner == 0)
                        owner = gridOwner;

                    if (block.CurrentDamage > 0 || block.HasDeformation) {

                        block.ClearConstructionStockpile(null);
                        block.IncreaseMountLevel(block.MaxIntegrity, owner, null, 10000, true);

                        MyCubeBlock cubeBlock = block.FatBlock;

                        if (cubeBlock != null) {

                            grid.ChangeOwnerRequest(grid, cubeBlock, 0, MyOwnershipShareModeEnum.Faction);
                            if (owner != 0)
                                grid.ChangeOwnerRequest(grid, cubeBlock, owner, MyOwnershipShareModeEnum.Faction);
                        }
                    }
                }
            }

            return true;
        }

        public static void TransferBlocksToBigOwner(HashSet<long> removedPlayers) {

            foreach (var entity in MyEntities.GetEntities()) {

                if (!(entity is MyCubeGrid grid))
                    continue;

                var newOwner = grid.BigOwners.FirstOrDefault();

                /* If new owner is nobody we share with all */
                var share = newOwner == 0 ? MyOwnershipShareModeEnum.All : MyOwnershipShareModeEnum.Faction;

                foreach (var block in grid.GetFatBlocks()) {

                    /* Nobody and players which werent deleted are ignored */
                    if (block.OwnerId == 0 || !removedPlayers.Contains(block.OwnerId))
                        continue;

                    grid.ChangeOwnerRequest(grid, block, 0, MyOwnershipShareModeEnum.Faction);
                    if (newOwner != 0)
                        grid.ChangeOwnerRequest(grid, block, newOwner, MyOwnershipShareModeEnum.Faction);
                }
            }
        }
    }
}
