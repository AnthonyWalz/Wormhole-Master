using ProtoBuf;
using VRageMath;

namespace Wormhole.Managers
{
    [ProtoContract]
    public class GateDataMessage
    {
        [ProtoMember(1)] public uint Id { get; set; }
        [ProtoMember(2)] public Vector3D Position { get; set; }
        [ProtoMember(3)] public Vector3D Forward { get; set; }
        [ProtoMember(4)] public float Size { get; set; }
        [ProtoMember(5)] public string ParticleId { get; set; }
        [ProtoMember(6)] public Vector3D Destination { get; set; }
    }
}