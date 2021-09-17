using System.Linq;
using System.Xml.Serialization;
using Torch;
using Torch.Collections;
using Torch.Views;

namespace Wormhole
{
    public class Config : ViewModel
    {
        [XmlElement("WormholeGate")] public MtObservableList<WormholeGate> WormholeGates { get; set; } = new ();

        [Display(Name = "Save Server On Grid Exit", Description = "Warning! May Cause Lag")]
        public bool SaveOnExit { get; set; }

        [Display(Name = "Save Server On Grid Enter", Description = "Warning! May Cause Lag")]
        public bool SaveOnEnter { get; set; }

        [Display(Name = "Allow Faction Members")]
        public bool AllowInFaction { get; set; }

        [Display(Name = "Keep Connected Grids", Description = "Keep grids linked by connector")]
        public bool IncludeConnectedGrids { get; set; } = true;

        [Display(Name = "Keep projector blueprints")]
        public bool ExportProjectorBlueprints { get; set; }

        [Display(Name = "JumpDrive SubtypeId", Description = "SubtypeId of your jump drive/wormhole stabilizer")]
        public string JumpDriveSubId { get; set; } = "WormholeDrive";

        [Display(Name = "Server IP:Port (for reconnecting)")]
        public string ThisIp { get; set; } = string.Empty;

        public double GateRadius { get; set; } = 180;

        [Display(Name = "Folder", Description = "Must be shared across all torches")]
        public string Folder { get; set; }

        [Display(Name = "Tick Rate", Description = "Wormhole runs once out of x ticks")]
        public int Tick { get; set; } = 50;

        [Display(Name = "Respawn Players", Description = "Keep players in cryos/beds/cockpits")]
        public bool PlayerRespawn { get; set; } = true;

        [Display(Name = "Work With All Jump Drives")]
        public bool WorkWithAllJd { get; set; }

        public bool AutoSend { get; set; }

        [Display(Name = "Keep Ownership",
            Description =
                "Keep ownership & builtBy on blocks. If false, all blocks will be transferred to player that requested jump")]
        public bool KeepOwnership { get; set; }

        [Display(Name = "Gate Visuals", Description = "Gate visual effects on jump")]
        public bool GateVisuals { get; set; } = false;
    }
}