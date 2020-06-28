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
                Listservers.Items.Add(server);
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e) {
            Plugin.Save();
        }
        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            if (Xinput.Text != string.Empty && Yinput.Text != string.Empty && Zinput.Text != string.Empty) {
                var newserver = new Server() { Name = Nameinput.Text, Description = Descriptioninput.Text, HexColor = HexColorinput.Text, IP = IPinput.Text, InFolder = Infileinput.Text, OutFolder = Outfileinput.Text, X = Convert.ToDouble(Xinput.Text), Y = Convert.ToDouble(Yinput.Text), Z = Convert.ToDouble(Zinput.Text) };
                if (Plugin.Config.WormholeServer.IndexOf(newserver) < 0 && Listservers.Items.IndexOf(newserver) < 0)
                {
                    Plugin.Config.WormholeServer.Add(newserver);
                    Listservers.Items.Add(newserver);
                    Nameinput.Text = string.Empty;
                    Descriptioninput.Text = string.Empty;
                    HexColorinput.Text = string.Empty;
                    IPinput.Text = string.Empty;
                    Infileinput.Text = string.Empty;
                    Outfileinput.Text = string.Empty;
                    Xinput.Text = string.Empty;
                    Yinput.Text = string.Empty;
                    Zinput.Text = string.Empty;
                }
            }
        }
        private void Del_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Config.WormholeServer.Remove(Listservers.SelectedItem as Server);
            Listservers.Items.Remove(Listservers.SelectedItem);
        }

        private void Edit_OnClick(object sender, RoutedEventArgs e)
        {
            if (Listservers.SelectedItem != null) { 
                Nameinput.Text = (Listservers.SelectedItem as Server).Name;
                Descriptioninput.Text = (Listservers.SelectedItem as Server).Description;
                HexColorinput.Text = (Listservers.SelectedItem as Server).HexColor;
                IPinput.Text = (Listservers.SelectedItem as Server).IP;
                Infileinput.Text = (Listservers.SelectedItem as Server).InFolder;
                Outfileinput.Text = (Listservers.SelectedItem as Server).OutFolder;
                Xinput.Text = (Listservers.SelectedItem as Server).X.ToString();
                Yinput.Text = (Listservers.SelectedItem as Server).Y.ToString();
                Zinput.Text = (Listservers.SelectedItem as Server).Z.ToString();
                Plugin.Config.WormholeServer.Remove(Listservers.SelectedItem as Server);
                Listservers.Items.Remove(Listservers.SelectedItem);
            }
        }

        private void GithubLink(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/AnthonyWalz/Wormhole-Master");
        }

        private void DiscordLink(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/zzxt2Zm");
        }

        private void PatreonLink(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.patreon.com/PrincessKennyCoding");
        }

        private void LordTylusGithubLink(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/LordTylus");
        }
    }
}
