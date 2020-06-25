using System.Linq;
using System.Xml.Serialization;
using Torch;
using Torch.Collections;

namespace Wormhole {
    public class WormholeConfig : ViewModel
    {
        [XmlIgnore]
        public MtObservableList<Server> WormholeServer { get; } = new MtObservableList<Server>();

        [XmlElement("WormholeServer")]
        public Server[] WormholeServer_xml
        {
            get
            {
                return this.WormholeServer.ToArray<Server>();
            }
            set
            {
                this.WormholeServer.Clear();
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        Server Server = value[i];
                        this.WormholeServer.Add(Server);
                    }
                }
            }
        }
        private bool _allowInFaction = false;
        private bool _keepOriginalOwner = false;
        private bool _includeConnectedGrids = true;
        private bool _exportProjectorGrids = false;

        private string _jumpDriveSubid = "LargeJumpDrive";

        private double _inRadiusGate = 50;
        private double _outInsideRadiusGate = 100;
        private double _outOutsideRadiusGate = 500;
        private int _tick = 500;
        private bool _beaconOverwrite = true;


        public bool AllowInFaction { get => _allowInFaction; set => SetValue(ref _allowInFaction, value); }
        public bool KeepOriginalOwner { get => _keepOriginalOwner; set => SetValue(ref _keepOriginalOwner, value); }
        public bool IncludeConnectedGrids { get => _includeConnectedGrids; set => SetValue(ref _includeConnectedGrids, value); }
        public bool ExportProjectorBlueprints { get => _exportProjectorGrids; set => SetValue(ref _exportProjectorGrids, value); }

        public string JumpDriveSubid { get => _jumpDriveSubid; set => SetValue(ref _jumpDriveSubid, value); }

        public double InRadiusGate { get => _inRadiusGate; set => SetValue(ref _inRadiusGate, value); }
        public double OutInsideRadiusGate { get => _outInsideRadiusGate; set => SetValue(ref _outInsideRadiusGate, value); }
        public double OutOutsideRadiusGate { get => _outOutsideRadiusGate; set => SetValue(ref _outOutsideRadiusGate, value); }
        public int Tick { get => _tick; set => SetValue(ref _tick, value); }
        public bool BeaconOverwrite { get => _beaconOverwrite; set => SetValue(ref _beaconOverwrite, value); }
    }
}