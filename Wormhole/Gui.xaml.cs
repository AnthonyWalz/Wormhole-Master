using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Wormhole
{
    public partial class Gui : UserControl
    {
        public Gui()
        {
            InitializeComponent();
        }

        public Gui(Plugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
            Listservers.ItemsSource = Plugin.Config.WormholeGates;
        }

        private Plugin Plugin { get; }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
            //Utilities.WormholeGateConfigUpdate();
        }

        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(Xinput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var xCord) ||
                !double.TryParse(Yinput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var yCord) ||
                !double.TryParse(Zinput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var zCord))
            {
                MessageBox.Show("Invalid Coordinates");
                return;
            }

            var newServer = new WormholeGate
            {
                Name = Nameinput.Text,
                Description = Descriptioninput.Text,
                HexColor = HexColorinput.Text,
                SendTo = SendToinput.Text,
                X = xCord,
                Y = yCord,
                Z = zCord
            };

            if (Plugin.Config.WormholeGates.Contains(newServer)) return;

            Plugin.Config.WormholeGates.Add(newServer);
            Nameinput.Text = string.Empty;
            Descriptioninput.Text = string.Empty;
            HexColorinput.Text = string.Empty;
            SendToinput.Text = string.Empty;
            Xinput.Text = string.Empty;
            Yinput.Text = string.Empty;
            Zinput.Text = string.Empty;
        }

        private void Del_OnClick(object sender, RoutedEventArgs e)
        {
            if (Listservers.SelectedItem is WormholeGate gate)
                Plugin.Config.WormholeGates.Remove(gate);
        }

        private void Edit_OnClick(object sender, RoutedEventArgs e)
        {
            if (Listservers.SelectedItem is not WormholeGate gate) return;

            Nameinput.Text = gate.Name;
            Descriptioninput.Text = gate.Description;
            HexColorinput.Text = gate.HexColor;
            SendToinput.Text = gate.SendTo;
            Xinput.Text = gate.X.ToString(CultureInfo.InvariantCulture);
            Yinput.Text = gate.Y.ToString(CultureInfo.InvariantCulture);
            Zinput.Text = gate.Z.ToString(CultureInfo.InvariantCulture);
            Plugin.Config.WormholeGates.Remove(gate);
        }
    }
}