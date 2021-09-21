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
        private static readonly XmlSerializer RequestSerializer = new (typeof(Request));

        [Dependency] private readonly InstanceManager _instanceManager = null!;
        
        private readonly IList<MyDefinitionId> _jdDefinitions = new List<MyDefinitionId>();
        private bool _isModPresent;
        
        public DestinationManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public override void Attach()
        {
            base.Attach();
            foreach (var jd in Plugin.Instance.Config.JumpDriveSubId.Split(','))
            {
                _jdDefinitions.Add(MyDefinitionId.Parse($"{nameof(MyObjectBuilder_JumpDrive)}/{jd}"));
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
            return Plugin.Instance.Config.WorkWithAllJd || _jdDefinitions.Contains(drive.BlockDefinition.Id);
        }
        
        // ReSharper disable once SuggestBaseTypeForParameter
        public DestinationViewModel TryGetDestination(MyTerminalBlock block, GateViewModel gate)
        {
            if (TryGetDestinationFromStorage(block.Storage, out var destinationId))
                return gate.Destinations.SingleOrDefault(b => b.Id == destinationId);
            
            if (TryGetDestinationLegacy(block, gate.Destinations, out var destination))
                return gate.Destinations.SingleOrDefault(b => b.DisplayName == destination);
            
            return null;
        }

        private static bool TryGetDestinationFromStorage(MyModStorageComponentBase component, out string destination)
        {
            destination = null;
            
            if (!(component?.TryGetValue(ComponentGuid, out destination) ?? false)) 
                return false;
            
            component.Remove(ComponentGuid);
            return true;

        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static bool TryGetDestinationLegacy(MyTerminalBlock block, IEnumerable<DestinationViewModel> destinations, out string destination)
        {
            destination = default;
            if (string.IsNullOrEmpty(block.CustomData))
                return false;

            try
            {
                using var reader = new StringReader(block.CustomData);
                if (RequestSerializer.Deserialize(reader) is not Request {PluginRequest: true, Destination: { }} request)
                {
                    if (!string.IsNullOrEmpty(block.CustomData)) return false;
                    using var writer = new StringWriter();
                    RequestSerializer.Serialize(writer, new Request
                    {
                        PluginRequest = false,
                        Destination = null,
                        Destinations = destinations.Select(static b => b.DisplayName).ToArray()
                    });

                    block.CustomData = writer.ToString();
                    return false;
                }
            
                destination = request.Destination;
            }
            catch
            {
                return false;
            }
            
            block.CustomData = string.Empty;
            return true;
        }
    }
}