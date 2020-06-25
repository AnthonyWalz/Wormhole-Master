using Sandbox.Game.Entities;
using System.Collections.Generic;
using VRage.Game.Entity;

namespace Wormhole.Utils {

    public class OwnershipUtils {

        public static long GetOwner(MyCubeGrid grid) {

            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                return gridOwnerList[0];
            else if (ownerCnt > 1)
                return gridOwnerList[1];

            return gridOwner;
        }

        public static Dictionary<long, BuildStats> FindBuildStatsPerPlayer() {

            Dictionary<long, BuildStats> stats = new Dictionary<long, BuildStats>();
            
            foreach(MyEntity entity in MyEntities.GetEntities()) { 

                if (!(entity is MyCubeGrid grid))
                    continue;

                foreach(var block in grid.GetBlocks()) { 

                    long buildBy = block.BuiltBy;

                    if(!stats.TryGetValue(buildBy, out BuildStats statsForPlayer)) {
                        statsForPlayer = new BuildStats();
                        stats.Add(buildBy, statsForPlayer);
                    }

                    statsForPlayer.BlockCount++;
                    statsForPlayer.PcuCount += BlockUtils.GetPcu(block);
                }
            }

            return stats;
        }

        public class BuildStats {

            public int PcuCount { get; set; }
            public int BlockCount { get; set; }
        }
    }
}
