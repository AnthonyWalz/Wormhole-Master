using System.Xml.Serialization;
using Torch;
using Torch.Collections;
using Torch.Views;
using Wormhole.ViewModels;

namespace Wormhole
{
    public class Config : ViewModel
    {
        [XmlArrayItem("Gate")] public MtObservableList<GateViewModel> WormholeGates { get; set; } = new();

        [Display(Name = "Save Server On Grid Exit", Description = "Warning! May Cause Lag")]
        public bool SaveOnExit { get; set; }

        [Display(Name = "Save Server On Grid Enter", Description = "Warning! May Cause Lag")]
        public bool SaveOnEnter { get; set; }

        [Display(Name = "Allow Faction Members")]
        public bool AllowInFaction { get; set; }

        [Display(Name = "Keep Connected Grids", Description = "Keep grids linked by connector/landing gear")]
        public bool IncludeConnectedGrids { get; set; } = true;

        [Display(Name = "Keep projector blueprints")]
        public bool ExportProjectorBlueprints { get; set; } = true;

        [Display(Name = "JumpDrive SubtypeId", Description = "SubtypeId of your jump drive/wormhole stabilizer")]
        public string JumpDriveSubId { get; set; } = "WormholeDrive, WormholeDrive_Small";

        [Display(Name = "Current Server IP:Port (domains also works)")]
        public string ThisIp { get; set; } = string.Empty;

        public double GateRadius { get; set; } = 180;

        [Display(Name = "Folder", Description = "Must be shared across all torches")]
        public string Folder { get; set; } = "WormHole";

        [Display(Name = "Tick Rate", Description = "Wormhole runs once out of x ticks")]
        public int Tick { get; set; } = 250;

        [Display(Name = "Respawn Players", Description = "Keep players in cryos/beds/cockpits")]
        public bool PlayerRespawn { get; set; } = true;

        [Display(Name = "Work With All Jump Drives")]
        public bool WorkWithAllJd { get; set; }

        public bool AutoSend { get; set; }

        [Display(Name = "Keep Ownership", Description = "Keep ownership & builtBy on blocks. If false, all blocks will be transferred to player that requested jump")]
        public bool KeepOwnership { get; set; }

        [Display(Name = "Jump Out Notification", Description = "Send Chat Notification when player Jump to other server.")]
        public string JumpOutNotification { get; set; } = "Player {PlayerName} used WormHole to go to {JumpTo} Bye!, Игрок {PlayerName} использовал Червоточину, чтобы прыгнуть в {JumpTo} Пока!";

        [Display(Name = "Jump In Notification", Description = "Send Chat Notification when player Jump to this server.")]
        public string JumpInNotification { get; set; } = "Player {PlayerName} arrived to this server, Welcome!, Игрок {PlayerName} прибыл на этот сервер, добро пожаловать!";

        [Display(Name = "Min Arrival Distance From Gate", Description = "Minimum spawn distance (metters) from gate when arriving from other server, must be less than MAX!, Server to Server Only.")]
        public int MinDistance { get; set; } = 1;

        [Display(Name = "Max Arrival Distance From Gate", Description = "Maximum spawn distance (metters) from gate when arriving from other server, must be more then MIN!, Server to Server Only.")]
        public int MaxDistance { get; set; } = 5;
    }
}