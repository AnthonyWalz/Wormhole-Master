using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Torch.API;
using Torch.Managers;
using Torch.Server.Managers;
using VRage.Game;
using VRage.Game.Components;
using Wormhole.ViewModels;

namespace Wormhole.Managers
{
    public class DestinationManager : Manager
    {
        private const ulong ModId = 2603356015;
        private static readonly Guid ComponentGuid = new ("7314ed35-7fe1-4652-9329-f525facdd32a");

        [Dependency] private readonly InstanceManager _instanceManager = null!;
        
        public readonly IList<MyDefinitionId> JdDefinitions = new List<MyDefinitionId>();
        private bool _isModPresent;
        
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
            Torch.GameStateChanged += TorchOnGameStateChanged;
        }

        private void TorchOnGameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (newState != TorchGameState.Loading || _instanceManager?.DedicatedConfig?.SelectedWorld?.WorldConfiguration is not {} configuration)
                return;
            _isModPresent = configuration.Mods.Exists(static b => b.PublishedFileId == ModId);
        }

        public bool IsValidJd(MyJumpDrive drive)
        {
            return Plugin.Instance.Config.WorkWithAllJd || JdDefinitions.Contains(drive.BlockDefinition.Id);
        }
        
        // ReSharper disable once SuggestBaseTypeForParameter
        public DestinationViewModel TryGetDestination(MyTerminalBlock block, GateViewModel gate)
        {
            return TryGetDestinationFromStorage(block.Storage, out var destinationId)
                ? gate.Destinations.SingleOrDefault(b => b.Id == destinationId)
                : null;
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