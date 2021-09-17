using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Wormhole.Mod
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class JumpComponent : MySessionComponentBase
    {
        private static readonly MySoundPair ChargeSound = new MySoundPair("WormholeJumpCharge");
        private static readonly MySoundPair JumpSound = new MySoundPair("WormholeJumpPerform");
        private static readonly MySoundPair AfterSound = new MySoundPair("WormholeJumpAfter");
        
        private readonly Dictionary<uint, GateDataMessage> _gates = new Dictionary<uint, GateDataMessage>();
        private readonly MyEntity3DSoundEmitter _soundEmitter = new MyEntity3DSoundEmitter(null);
        
        private GateVisuals _gateVisuals => GateVisuals.Instance;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Multiplayer.IsServer) return;
            base.Init(sessionComponent);
            RegisterHandlers();
        }

        public override void BeforeStart()
        {
            if (MyAPIGateway.Multiplayer.IsServer) return;
            base.BeforeStart();
            RequestGatesData();
        }

        #region Netowrking

        private const ushort JumpStatusNetId = 3456;
        private const ushort GateDataNetId = 3457;
        
        private void RegisterHandlers()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(JumpStatusNetId, JumpStatusHandler);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GateDataNetId, GateDataHandler);
        }

        private static void RequestGatesData()
        {
            MyAPIGateway.Multiplayer.SendMessageToServer(GateDataNetId, new byte[1]);
        }

        private void JumpStatusHandler(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<JumpStatusMessage>(data);
            IMyEntity entity;
            IMyCubeGrid grid;
            GateDataMessage gate;
            if (message == null || !fromServer || !MyAPIGateway.Entities.TryGetEntityById(message.GridId, out entity) ||
                (grid = entity as IMyCubeGrid) == null || !_gates.TryGetValue(message.GateId, out gate))
                return;
            MyLog.Default?.WriteLine($"Jump status update {message.Status}");
            switch (message.Status)
            {
                case JumpStatus.Started:
                    OnJumpStarted(gate, grid);
                    break;
                case JumpStatus.Ready:
                    OnJumpReady(gate, grid);
                    break;
                case JumpStatus.Perform:
                    OnJumpPerform(gate, grid, message.Destination);
                    break;
                case JumpStatus.Succeeded:
                    OnJumpSucceeded(gate, grid);
                    break;
                default:
                    throw new Exception("Out of Range");
            }
        }

        private void GateDataHandler(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<GatesMessage>(data);
            if (message == null || !fromServer)
                return;
            MyLog.Default?.WriteLine($"Loaded {message.Messages} gates");
            OnGatesData(message.Messages);
        }
        #endregion

        private void OnJumpStarted(GateDataMessage gate, IMyCubeGrid grid)
        {
            _gateVisuals.EnableEffectForGate(gate.Id);
            _soundEmitter.Entity = (MyEntity) grid;
            _soundEmitter.PlaySound(ChargeSound);
            
            // if (MyAPIGateway.Session.ControlledObject is IMyShipController &&
            //     ((IMyShipController)MyAPIGateway.Session.ControlledObject).CubeGrid == grid)
            // {
            //     MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.SpectatorFixed,
            //         position: gate.Position + gate.Forward * (gate.Size * 2));
            // }
        }

        private void OnJumpReady(GateDataMessage gate, IMyCubeGrid grid)
        {
            _soundEmitter.PlaySound(JumpSound, true);
        }
        private void OnJumpPerform(GateDataMessage gate, IMyCubeGrid grid, Vector3D destination)
        {
            if (Vector3D.IsZero(destination)) return;
            var matrix = grid.WorldMatrix;
            matrix.Translation = destination;
            grid.Teleport(matrix);
            
            // if (MyAPIGateway.Session.ControlledObject is IMyShipController &&
            //     ((IMyShipController)MyAPIGateway.Session.ControlledObject).CubeGrid == grid)
            // {
            //     MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.Entity, grid);
            // }
        }
        private void OnJumpSucceeded(GateDataMessage gate, IMyCubeGrid grid)
        {
            _gateVisuals.DisableEffectForGate(gate.Id);
            if (MyAPIGateway.Session.ControlledObject is IMyShipController &&
                ((IMyShipController)MyAPIGateway.Session.ControlledObject).CubeGrid == grid)
            {
                _soundEmitter.PlaySound(AfterSound, true);
            }
        }

        private void OnGatesData(IEnumerable<GateDataMessage> gates)
        {
            _gates.Clear();
            foreach (var gate in gates)
            {
                _gates[gate.Id] = gate;
                _gateVisuals.CreateEffectForGate(gate);
            }
        }
    }
}