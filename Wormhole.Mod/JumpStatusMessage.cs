using ProtoBuf;
using VRageMath;

namespace Wormhole.Mod
{
    [ProtoContract]
    public class JumpStatusMessage
    {
        [ProtoMember(1)]
        public uint GateId { get; set; }
        [ProtoMember(2)]
        public JumpStatus Status { get; set; }
        [ProtoMember(3)]
        public long GridId { get; set; }
        [ProtoMember(4)]
        public Vector3D Destination { get; set; }
    }
}