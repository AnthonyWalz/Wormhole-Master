using ProtoBuf;
using VRage.ObjectBuilders;

namespace Wormhole
{
    [ProtoContract]
    public class Request
    {
        [ProtoMember(1)]
        public bool PluginRequest { get; set; }
        [ProtoMember(2)]
        public string[] Destinations { get; set; }
        [ProtoMember(3)]
        public string Destination { get; set; }
    }
}