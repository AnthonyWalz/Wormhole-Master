using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wormhole.Utils {
    public class BlockUtils {

        public static int GetPcu(MySlimBlock block) {

            int pcuValue = 1;
            if (block.ComponentStack.IsFunctional)
                pcuValue = block.BlockDefinition.PCU;

            return pcuValue;
        }
    }
}
