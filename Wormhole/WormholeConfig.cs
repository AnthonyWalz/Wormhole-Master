using System.Linq;
using System.Xml.Serialization;
using Torch;
using Torch.Collections;

namespace Wormhole {
    public class Config : ViewModel
    {
        [XmlIgnore]
        public MtObservableList<WormholeGate> WormholeGates { get; } = new MtObservableList<WormholeGate>();
        [XmlElement("WormholeGate")]
        public WormholeGate[] WormholeGates_xml
        {
            get => WormholeGates.ToArray();
            set
            {
                WormholeGates.Clear();
                if (value == null)
                {
                    return;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    WormholeGate Wormhole = value[i];
                    WormholeGates.Add(Wormhole);
                }
            }
        }

        private bool _saveOnExit = false;
        private bool _saveOnEnter = false;
        private bool _allowInFaction = false;
        private bool _includeConnectedGrids = true;
        private bool _exportProjectorGrids = false;

        private string _jumpDriveSubid = "WormholeDrive";
        private string _thisIp = "";

        private double _radiusGate = 180;
        private string _folder = "";
        private int _tick = 50;
        private bool _playerRespawn = true;
        private bool _workWithAllJD = false;
        private bool _autoSend = false;
        private bool[] _playerRespawnType = new bool[] {false, false};

        public bool[] PlayerRespawnType { get => _playerRespawnType; set => SetValue(ref _playerRespawnType, value); }
        public bool SaveOnExit { get => _saveOnExit; set => SetValue(ref _saveOnExit, value); }
        public bool SaveOnEnter { get => _saveOnEnter; set => SetValue(ref _saveOnEnter, value); }
        public bool AllowInFaction { get => _allowInFaction; set => SetValue(ref _allowInFaction, value); }
        public bool IncludeConnectedGrids { get => _includeConnectedGrids; set => SetValue(ref _includeConnectedGrids, value); }
        public bool ExportProjectorBlueprints { get => _exportProjectorGrids; set => SetValue(ref _exportProjectorGrids, value); }

        public string JumpDriveSubid { get => _jumpDriveSubid; set => SetValue(ref _jumpDriveSubid, value); }
        public string ThisIp { get => _thisIp; set => SetValue(ref _thisIp, value); }

        public double RadiusGate { get => _radiusGate; set => SetValue(ref _radiusGate, value); }
        public string Folder { get => _folder; set => SetValue(ref _folder, value); }

        public int Tick { get => _tick; set => SetValue(ref _tick, value); }
        public bool PlayerRespawn { get => _playerRespawn; set => SetValue(ref _playerRespawn, value); }
        public bool WorkWithAllJD { get => _workWithAllJD; set => SetValue(ref _workWithAllJD, value); }
        public bool AutoSend { get => _autoSend; set => SetValue(ref _autoSend, value); }
    }
}