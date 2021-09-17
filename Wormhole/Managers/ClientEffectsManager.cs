using System.Collections.Generic;
using System.Linq;
using NLog.Fluent;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API;
using Torch.Managers;
using VRage.Game.Entity;
using VRage.Security;
using VRageMath;

namespace Wormhole.Managers
{
    public class ClientEffectsManager : Manager
    {
        private const ushort JumpStatusNetId = 3456;
        private const ushort GateDataNetId = 3457;
        private const string ParticleDefaultId = "p_subspace_start";

        private readonly Dictionary<WormholeGate, GateDataMessage> _visualData = new ();
        private readonly GatesMessage _message = new ();

        public ClientEffectsManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public override void Attach()
        {
            base.Attach();
            Torch.GameStateChanged += TorchOnGameStateChanged;
        }

        public override void Detach()
        {
            base.Detach();
            Torch.GameStateChanged -= TorchOnGameStateChanged;
        }

        public void NotifyJumpStatusChanged(JumpStatus status, WormholeGate gate, MyCubeGrid grid,
            Vector3D? destination = null)
        {
            var message = new JumpStatusMessage
            {
                Status = status,
                GateId = gate.Id,
                GridId = grid.EntityId,
                Destination = destination ?? _visualData[gate].Destination
            };

            MyAPIGateway.Multiplayer.SendMessageToOthers(JumpStatusNetId,
                MyAPIGateway.Utilities.SerializeToBinary(message));
        }


        public void RecalculateVisualData()
        {
            if (Torch.GameState != TorchGameState.Loaded) return;

            _visualData.Clear();
            _message.Messages.Clear();
            var config = Plugin.Instance.Config;
            var entities = new List<MyEntity>();
            foreach (var wormholeGate in config.WormholeGates)
            {
                var sphere = new BoundingSphereD(wormholeGate.Position, config.GateRadius);
                entities.Clear();

                MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, entities);

                var gate = entities.OfType<MyCubeGrid>()
                    .FirstOrDefault(static b => b.DisplayName.Contains("[NPC-IGNORE]_[Wormhole-Gate]"));

                var destinationName = wormholeGate.SendTo.Split(':')[0];

                GateDataMessage visual;

                if (gate is { })
                    visual = new ()
                    {
                        Id = wormholeGate.Id,
                        Position = gate.PositionComp.GetPosition(),
                        Forward = gate.PositionComp.WorldMatrixRef.Forward,
                        Size = (float) config.GateRadius,
                        ParticleId = ParticleDefaultId,
                        Destination = config.WormholeGates.FirstOrDefault(b => b.Name == destinationName)?.Position ??
                                      Vector3D.Zero
                    };
                else
                    visual = new ()
                    {
                        Id = wormholeGate.Id,
                        Position = wormholeGate.Position,
                        Forward = Vector3D.Forward,
                        Size = (float) config.GateRadius,
                        ParticleId = ParticleDefaultId,
                        Destination = config.WormholeGates.FirstOrDefault(b => b.Name == destinationName)?.Position ??
                                      Vector3D.Zero
                    };

                visual.Size = (float) config.GateRadius;
                _visualData[wormholeGate] = visual;
                _message.Messages.Add(visual);
            }

            MyAPIGateway.Multiplayer.SendMessageToOthers(GateDataNetId,
                MyAPIGateway.Utilities.SerializeToBinary(_message));
        }

        private void TorchOnGameStateChanged(MySandboxGame game, TorchGameState newState)
        {
            if (newState != TorchGameState.Loaded) return;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GateDataNetId, GateDataHandler);
            RecalculateVisualData();
        }

        private void GateDataHandler(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            if (data.Length != 1 || fromServer)
            {
                Plugin.Log.Warn($"Invalid gates request from {sender}");
            }
            else
            {
                Plugin.Log.Info($"Gates request from {sender}");
                MyAPIGateway.Multiplayer.SendMessageTo(GateDataNetId,
                    MyAPIGateway.Utilities.SerializeToBinary(_message),
                    sender);
            }
        }
    }
}