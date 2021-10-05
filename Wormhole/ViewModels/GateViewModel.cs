using System.Xml.Serialization;
using Sandbox.Game.Screens.Helpers;
using Torch;
using Torch.Collections;
using Torch.Utils;
using VRage.Security;
using VRageMath;

namespace Wormhole.ViewModels
{
    public class GateViewModel : ViewModel
    {
        private uint? _id;
        public string Name { get; set; }
        public string Description { get; set; }
        public string HexColor { get; set; }
        [XmlArrayItem("Destination")]
        public MtObservableList<DestinationViewModel> Destinations { get; set; } = new();
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        [XmlIgnore] public Vector3D Position => new(X, Y, Z);

        [XmlIgnore] public uint Id => _id ??= FnvHash.Compute(Name);

        public MyGps ToGps()
        {
            return new()
            {
                Name = Name,
                Description = Description ?? string.Empty,
                GPSColor = ColorUtils.TranslateColor(HexColor),
                Coords = Position,
                ShowOnHud = true,
                AlwaysVisible = true
            };
        }
    }
}