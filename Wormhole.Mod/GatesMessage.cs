using System.Collections.Generic;
using ProtoBuf;
using VRage.ObjectBuilders;

namespace Wormhole.Mod
{
    [ProtoContract]
    public class GatesMessage
    {
        [ProtoMember(1)] public List<GateDataMessage> Messages { get; set; } = new List<GateDataMessage>();
        [ProtoMember(2)] public List<SerializableDefinitionId> WormholeDriveIds { get; set; } = new List<SerializableDefinitionId>();
        [ProtoMember(3)] public bool WorkWithAllJds { get; set; }
    }
}