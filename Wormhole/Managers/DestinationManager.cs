using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Torch.API;
using Torch.Managers;
using VRage.Game;
using VRage.Game.Components;
using Wormhole.ViewModels;

namespace Wormhole.Managers
{
    public class DestinationManager : Manager
    {
        private static readonly Guid ComponentGuid = new("7314ed35-7fe1-4652-9329-f525facdd32a");
        public readonly IList<MyDefinitionId> JdDefinitions = new List<MyDefinitionId>();

        public DestinationManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public override void Attach()
        {
            base.Attach();
            foreach (var jd in Plugin.Instance.Config.JumpDriveSubId.Split(','))
            {
                JdDefinitions.Add(MyDefinitionId.Parse($"{nameof(MyObjectBuilder_JumpDrive)}/{jd}"));
            }
        }

        public bool IsValidJd(MyJumpDrive drive)
        {
            return Plugin.Instance.Config.WorkWithAllJd || JdDefinitions.Contains(drive.BlockDefinition.Id);
        }

        public DestinationViewModel TryGetDestination(MyTerminalBlock block, GateViewModel gate)
        {
            return TryGetDestinationFromStorage(block.Storage, out var destinationId) ? gate.Destinations.SingleOrDefault(b => b.Id == destinationId) : null;
        }

        private static bool TryGetDestinationFromStorage(MyModStorageComponentBase component, out string destination)
        {
            destination = null;

            if (!(component?.TryGetValue(ComponentGuid, out destination) ?? false))
                return false;

            component.Remove(ComponentGuid);
            return true;
        }
    }
}