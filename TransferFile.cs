using System.Collections.Generic;
using ProtoBuf;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Serialization;

namespace Wormhole
{
    [ProtoContract]
    public class TransferFile
    {
        [ProtoMember(1)]
        public List<MyObjectBuilder_CubeGrid> Grids;
        [ProtoMember(2)]
        public Dictionary<long, MyObjectBuilder_Identity> IdentitiesMap;
        [ProtoMember(3)]
        public Dictionary<long, ulong> PlayerIdsMap;
    }
}