using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Wormhole
{
    public class Request
    {
        public bool PluginRequest { get; set; }
        public string[] Destinations { get; set; }
        public string Destination { get; set; }
    }
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), true, "WormholeDrive", "WormholeDrive_Small")]
    public class Main : MyGameLogicComponent
    {

        public bool loaded = false;
        public static bool customoptions = false;
        public static MyTerminalControlListBoxItem Selected;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session != null && !MyAPIGateway.Multiplayer.IsServer)
            {
                if (!loaded)
                {
                    List<IMyTerminalControl> terminalcontrols = new List<IMyTerminalControl>();
                    MyAPIGateway.TerminalControls.CustomControlGetter += Controls;
                    MyAPIGateway.TerminalControls.CustomActionGetter += Actions;

                    var controlList = new List<IMyTerminalControl>();
                    MyAPIGateway.TerminalControls.GetControls<IMyJumpDrive>(out controlList);

                    loaded = true;
                }
            }
        }
        public static void Controls(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block as IMyJumpDrive != null)
            {
                var controlList = new List<IMyTerminalControl>();
                MyAPIGateway.TerminalControls.GetControls<IMyJumpDrive>(out controlList);
                foreach (IMyTerminalControl terminalcontrol in controlList)
                {
                    if (terminalcontrol.Id == "JumpDistance" || terminalcontrol.Id == "RemoveBtn" || terminalcontrol.Id == "SelectBtn" || terminalcontrol.Id == "GpsList" || terminalcontrol.Id == "SelectedTarget")
                    {
                        terminalcontrol.Visible = Block => Hide(block);
                    }
                }
                if (!customoptions)
                {
                    var WormholeSelected = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyJumpDrive>("Wormhole Selected");
                    WormholeSelected.Title = MyStringId.GetOrCompute("Wormhole Selected");
                    WormholeSelected.SupportsMultipleBlocks = false;
                    WormholeSelected.VisibleRowsCount = 1;
                    WormholeSelected.Multiselect = false;
                    WormholeSelected.ListContent = GetSelected;
                    WormholeSelected.Enabled = Block => true;
                    WormholeSelected.Visible = Block => Show(Block);
                    WormholeSelected.ItemSelected = SelectedItemSelected;
                    MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(WormholeSelected);
                    controls.Add(WormholeSelected);

                    var Refresh = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyJumpDrive>("Refresh List");
                    Refresh.Title = MyStringId.GetOrCompute("Refresh List");
                    Refresh.SupportsMultipleBlocks = false;
                    Refresh.Action = Block => CallRefresh(block);
                    Refresh.Enabled = Block => Show(Block);
                    Refresh.Visible = Block => Show(Block);
                    MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(Refresh);
                    controls.Add(Refresh);

                    var WormholeList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyJumpDrive>("Wormhole List");
                    WormholeList.Title = MyStringId.GetOrCompute("Wormhole List");
                    WormholeList.SupportsMultipleBlocks = false;
                    WormholeList.VisibleRowsCount = 12;
                    WormholeList.Multiselect = false;
                    WormholeList.ListContent = GetList;
                    WormholeList.Enabled = Block => true;
                    WormholeList.Visible = Block => Show(Block);
                    WormholeList.ItemSelected = SelectedItem;
                    MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(WormholeList);
                    controls.Add(WormholeList);

                    customoptions = true;
                }
            }
        }
        public static void Actions(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (block as IMyJumpDrive != null)
            {
                var actionList = new List<IMyTerminalAction>();
                MyAPIGateway.TerminalControls.GetActions<IMyJumpDrive>(out actionList);
                foreach (IMyTerminalAction terminalaction in actionList)
                {
                    if (terminalaction.Id == "Jump" || terminalaction.Id == "IncreaseJumpDistance" || terminalaction.Id == "DecreaseJumpDistance")
                    {
                        terminalaction.Enabled = Block => Hide(block);
                    }
                }
            }
        }
        public static bool Hide(IMyTerminalBlock block)
        {
            if ((block as IMyJumpDrive).BlockDefinition.SubtypeName == "WormholeDrive")
            {
                return false;
            }
            if ((block as IMyJumpDrive).BlockDefinition.SubtypeName == "WormholeDrive_Small")
            {
                return false;
            }
            return true;
        }
        public static bool Show(IMyTerminalBlock block)
        {
            if ((block as IMyJumpDrive).BlockDefinition.SubtypeName == "WormholeDrive")
            {
                return true;
            }
            if ((block as IMyJumpDrive).BlockDefinition.SubtypeName == "WormholeDrive_Small")
            {
                return true;
            }
            return false;
        }
        public static void CallRefresh(IMyTerminalBlock block)
        {
            Request Message = new Request
            {
                PluginRequest = true,
                Destinations = null,
                Destination = ""
            };
            block.CustomData = MyAPIGateway.Utilities.SerializeToXML<Request>(Message);
        }
        public static void SelectedItem(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems)
        {
            try
            {
                Request reply = MyAPIGateway.Utilities.SerializeFromXML<Request>(block.CustomData);
                var result = "";
                foreach (var destination in reply.Destinations)
                {
                    if (destination.Split(':')[0] == listItems.FirstOrDefault().Text.ToString())
                    {
                        result = destination;
                    }
                }
                Request Message = new Request
                {
                    PluginRequest = true,
                    Destinations = reply.Destinations,
                    Destination = result
                };
                block.CustomData = MyAPIGateway.Utilities.SerializeToXML<Request>(Message);
                listItems.Select(s => s.Text == Selected.Text);
            }
            catch { }
        }
        public static void SelectedItemSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems)
        {
            try
            {
                listItems.Select(s => s.Text == Selected.Text);
            }
            catch { }
        }
        public static void GetList(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems, List<MyTerminalControlListBoxItem> selectedItems)
        {
            try
            {
                Request reply = MyAPIGateway.Utilities.SerializeFromXML<Request>(block.CustomData);
                foreach (var destination in reply.Destinations)
                {
                    listItems.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(destination.Split(':')[0].Trim()), MyStringId.GetOrCompute(""), null));
                }
                selectedItems.Add(Selected);
            }
            catch { }
        }
        public static void GetSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> listItems, List<MyTerminalControlListBoxItem> selectedItems)
        {
            try
            {
                Request reply = MyAPIGateway.Utilities.SerializeFromXML<Request>(block.CustomData);
                listItems.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(reply.Destination.Split(':')[0].Trim()), MyStringId.GetOrCompute(""), null));
                selectedItems.Add(Selected);
            }
            catch { }
        }
    }
}