using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Wormhole {

    public partial class Control : UserControl {
        private WormholePlugin Plugin { get; }
        public Control() {
            InitializeComponent();
        }
        public Control(WormholePlugin plugin) : this() {
            Plugin = plugin;
            DataContext = plugin.Config;
            foreach (Server server in plugin.Config.WormholeServer.ToArray<Server>())
            {
                this.Listservers.Items.Add(server);
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e) {
            Plugin.Save();
        }
        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            this.Plugin.Config.WormholeServer.Add(new Server() { Name = Nameinput.Text, IP = IPinput.Text, InFolder = Infileinput.Text, OutFolder = Outfileinput.Text, X = Convert.ToDouble(Xinput.Text), Y = Convert.ToDouble(Yinput.Text), Z = Convert.ToDouble(Zinput.Text) });
            this.Listservers.Items.Add(new Server() { Name = Nameinput.Text, IP = IPinput.Text, InFolder = Infileinput.Text, OutFolder = Outfileinput.Text, X = Convert.ToDouble(Xinput.Text), Y = Convert.ToDouble(Yinput.Text), Z = Convert.ToDouble(Zinput.Text) });
            Nameinput.Text = string.Empty;
            IPinput.Text = string.Empty;
            Infileinput.Text = string.Empty;
            Outfileinput.Text = string.Empty;
            Xinput.Text = string.Empty;
            Yinput.Text = string.Empty;
            Zinput.Text = string.Empty;
        }
        private void Del_OnClick(object sender, RoutedEventArgs e)
        {
            this.Plugin.Config.WormholeServer.Remove(this.Listservers.SelectedItem as Server);
            this.Listservers.Items.Remove(this.Listservers.SelectedItem);
        }
    }
}
