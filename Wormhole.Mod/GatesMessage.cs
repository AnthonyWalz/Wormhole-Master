using System.Collections.Generic;
using ProtoBuf;

namespace Wormhole.Mod
{
    [ProtoContract]
    public class GatesMessage
    {
        [ProtoMember(1)]
        public List<GateDataMessage> Messages { get; set; } = new List<GateDataMessage>();
    }
}