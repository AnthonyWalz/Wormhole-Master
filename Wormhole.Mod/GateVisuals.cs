using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;

namespace Wormhole.Mod
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class GateVisuals : MySessionComponentBase
    {
        // The visual effects for the gates were designed by Klime who did a fantastic job.

        public static GateVisuals Instance;
        private readonly Dictionary<uint, RotatingParticle> _allEffects = new Dictionary<uint, RotatingParticle>();
        private readonly Dictionary<uint, RotatingParticle> _enabledEffects = new Dictionary<uint, RotatingParticle>();

        public GateVisuals()
        {
            Instance = this;
        }

        public bool CreateEffectForGate(GateDataMessage gate, bool enable = false)
        {
            var effectName = gate.ParticleId;
            var size = gate.Size;

            var centerMatrix = MatrixD.CreateWorld(gate.Position, Vector3D.CalculatePerpendicularVector(gate.Forward), - gate.Forward);

            if (effectName.Contains("end"))
            {
                size *= 0.4f;
                centerMatrix = MatrixD.CreateWorld(gate.Position, Vector3D.CalculatePerpendicularVector(gate.Forward), gate.Forward);
            }

            var initialMatrix = centerMatrix;
            initialMatrix.Translation += initialMatrix.Right * size;
            var wmPos = initialMatrix.Translation;
            var rotatingParticle = new RotatingParticle(0, initialMatrix, centerMatrix, size, gate.ParticleId);

            _allEffects[gate.Id] = rotatingParticle;
            if (enable)
            {
                _enabledEffects[gate.Id] = rotatingParticle;
                return true;
            }
            return true;
        }

        public void EnableEffectForGate(uint gateId)
        {
            RotatingParticle effect;
            if (!_allEffects.TryGetValue(gateId, out effect))
                return;

            _enabledEffects[gateId] = effect;
            effect.Play();
        }

        public void DisableEffectForGate(uint gateId)
        {
            RotatingParticle effect;
            if (!_enabledEffects.TryGetValue(gateId, out effect))
                return;

            _enabledEffects.Remove(gateId);
            effect.Stop();
        }

        private void RemoveAllEffect()
        {
            foreach (var effect in _allEffects)
            {
                var rotatingParticle = effect.Value;
                rotatingParticle.Stop();
            }

            _allEffects.Clear();
            _enabledEffects.Clear();
        }


        public override void Draw()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                return;

            foreach (var effect in _enabledEffects)
            {
                var rotatingParticle = effect.Value;

                if (rotatingParticle.Effect == null)
                {
                    rotatingParticle.Play();
                    continue;
                }

                //Rotate the particle emitter every tick
                var angle = Vector3D.Rotate(rotatingParticle.InitialMatrix.Translation - rotatingParticle.CenterMatrix.Translation,
                                            MatrixD.CreateFromAxisAngle(rotatingParticle.CenterMatrix.Up, rotatingParticle.CurrentAngle));

                var finalPos = rotatingParticle.CenterMatrix.Translation + Vector3D.Normalize(angle) * rotatingParticle.Radius;
                var finalUp = Vector3D.Normalize(rotatingParticle.CenterMatrix.Translation - finalPos);

                //Final matrix
                rotatingParticle.Effect.WorldMatrix = MatrixD.CreateWorld(finalPos, finalUp, rotatingParticle.Effect.WorldMatrix.Up);

                //Increase the angle every tick
                rotatingParticle.CurrentAngle += 0.5f;
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                return;

            RemoveAllEffect();
        }

        public class RotatingParticle
        {
            public MatrixD CenterMatrix; //Center position to orbit around
            public float CurrentAngle;
            public MyParticleEffect Effect;
            public MatrixD InitialMatrix; //Initial position
            public float Radius;
            private readonly string _effectName;

            public RotatingParticle(float currentAngle, MatrixD initialMatrix, MatrixD centerMatrix, float radius, string effectName)
            {
                CurrentAngle = currentAngle;
                InitialMatrix = initialMatrix;
                CenterMatrix = centerMatrix;
                Radius = radius;
                _effectName = effectName;
            }

            public void Play()
            {
                var pos = InitialMatrix.Translation;
                MyParticlesManager.TryCreateParticleEffect(_effectName, ref InitialMatrix, ref pos, uint.MaxValue, out Effect);
            }

            public void Stop()
            {
                Effect?.StopEmitting(10);
                Effect = null;
            }
        }
    }
}