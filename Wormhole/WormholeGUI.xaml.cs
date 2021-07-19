using System;
using System.Diagnostics;
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

        public Gui(WormholePlugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;
            foreach (var wormhole in plugin.Config.WormholeGates.ToArray()) Listservers.Items.Add(wormhole);
        }

        private WormholePlugin Plugin { get; }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Save();
            //Utilities.WormholeGateConfigUpdate();
        }

        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            if (Xinput.Text != string.Empty && Yinput.Text != string.Empty && Zinput.Text != string.Empty)
            {
                var newserver = new WormholeGate
                {
                    Name = Nameinput.Text, Description = Descriptioninput.Text, HexColor = HexColorinput.Text,
                    SendTo = SendToinput.Text, X = Convert.ToDouble(Xinput.Text), Y = Convert.ToDouble(Yinput.Text),
                    Z = Convert.ToDouble(Zinput.Text)
                };
                if (Plugin.Config.WormholeGates.IndexOf(newserver) < 0 && Listservers.Items.IndexOf(newserver) < 0)
                {
                    Plugin.Config.WormholeGates.Add(newserver);
                    Listservers.Items.Add(newserver);
                    Nameinput.Text = string.Empty;
                    Descriptioninput.Text = string.Empty;
                    HexColorinput.Text = string.Empty;
                    SendToinput.Text = string.Empty;
                    Xinput.Text = string.Empty;
                    Yinput.Text = string.Empty;
                    Zinput.Text = string.Empty;
                }
            }
        }

        private void Del_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Config.WormholeGates.Remove(Listservers.SelectedItem as WormholeGate);
            Listservers.Items.Remove(Listservers.SelectedItem);
        }

        private void Edit_OnClick(object sender, RoutedEventArgs e)
        {
            if (Listservers.SelectedItem != null)
            {
                Nameinput.Text = (Listservers.SelectedItem as WormholeGate).Name;
                Descriptioninput.Text = (Listservers.SelectedItem as WormholeGate).Description;
                HexColorinput.Text = (Listservers.SelectedItem as WormholeGate).HexColor;
                SendToinput.Text = (Listservers.SelectedItem as WormholeGate).SendTo;
                Xinput.Text = (Listservers.SelectedItem as WormholeGate).X.ToString();
                Yinput.Text = (Listservers.SelectedItem as WormholeGate).Y.ToString();
                Zinput.Text = (Listservers.SelectedItem as WormholeGate).Z.ToString();
                Plugin.Config.WormholeGates.Remove(Listservers.SelectedItem as WormholeGate);
                Listservers.Items.Remove(Listservers.SelectedItem);
            }
        }

        private void GithubLink(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/AnthonyWalz/Wormhole-Master");
        }

        private void DiscordLink(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/zzxt2Zm");
        }

        private void PatreonLink(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.patreon.com/PrincessKennyCoding");
        }

        private void LordTylusGithubLink(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/LordTylus");
        }

        private void AutoSend_Checked(object sender, RoutedEventArgs e)
        {
        }
    }
}