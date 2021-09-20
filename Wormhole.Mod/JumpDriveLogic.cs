using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Wormhole.Mod
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), true)]
    public class JumpDriveLogic : MyGameLogicComponent
    {
        private static bool _controlsCreated;
        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (_controlsCreated) return;
            _controlsCreated = true;
            
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(DestinationSelectNetId, MessageHandler);
            }
            
            if (MyAPIGateway.Multiplayer.IsServer) return;

            MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(
                MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyJumpDrive>(
                    "WormholeSeparator"));

            var listBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyJumpDrive>(
                "WormholeDestinationsListBox");
            listBox.Multiselect = false;
            listBox.VisibleRowsCount = 5;
            listBox.SupportsMultipleBlocks = false;
            listBox.ListContent = ListContent;
            listBox.ItemSelected = ItemSelected;
            MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(listBox);
        }

        private static void ItemSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            if (selected.Count != 1) return;
            MyAPIGateway.Multiplayer.SendMessageToServer(DestinationSelectNetId,
                MyAPIGateway.Utilities.SerializeToBinary(new ItemSelectedMessage
                {
                    EntityId = block.EntityId,
                    SelectedId = (string) selected[0].UserData
                }));
        }

        private static void ListContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            foreach (var gate in JumpComponent.Instance.Gates.Where(gate =>
                new BoundingSphereD(gate.Value.Position, gate.Value.Size).Intersects(block.CubeGrid.WorldAABB)))
            {
                items.AddRange(gate.Value.Destinations.Select(destination =>
                    new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(destination.DisplayName),
                        MyStringId.NullOrEmpty, destination.Id)));
            }
        }

        private static void MessageHandler(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<ItemSelectedMessage>(data);
            
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(message.EntityId, out entity)) return;
            
            if (entity.Storage == null)
                entity.Storage = new MyModStorageComponent();
                
            entity.Storage[ComponentGuid] = message.SelectedId;
        }
        
        private const ushort DestinationSelectNetId = 3458;
        private static readonly Guid ComponentGuid = new Guid("7314ed35-7fe1-4652-9329-f525facdd32a");
    }

    [ProtoContract]
    public class ItemSelectedMessage
    {
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public string SelectedId { get; set; }
    }
}