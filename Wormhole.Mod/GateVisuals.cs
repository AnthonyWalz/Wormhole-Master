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
                
            var centerMatrix = MatrixD.CreateWorld(gate.Position,
                Vector3D.CalculatePerpendicularVector(gate.Forward),
                -gate.Forward);

            if (effectName.Contains("end"))
            {
                size *= 0.4f;
                centerMatrix = MatrixD.CreateWorld(gate.Position,
                    Vector3D.CalculatePerpendicularVector(gate.Forward),
                    gate.Forward);
            }

            var initialMatrix = centerMatrix;

            initialMatrix.Translation += initialMatrix.Right * size;
            var wmPos = initialMatrix.Translation;

            MyParticleEffect effect;
            MyParticlesManager.TryCreateParticleEffect(effectName, ref initialMatrix, ref wmPos, uint.MaxValue,
                out effect);
            if (effect == null) return false;

            var rotatingParticle =
                new RotatingParticle(effect, 0, initialMatrix, centerMatrix, size);

            _allEffects[gate.Id] = rotatingParticle;
            if (enable)
            {
                _enabledEffects[gate.Id] = rotatingParticle;
                return true;
            }
            
            effect.Pause();
            return true;
        }

        public void EnableEffectForGate(uint gateId)
        {
            RotatingParticle effect;
            if (!_allEffects.TryGetValue(gateId, out effect))
                return;
            _enabledEffects[gateId] = effect;
            effect.Effect.Play();
        }
        
        public void DisableEffectForGate(uint gateId)
        {
            RotatingParticle effect;
            if (!_enabledEffects.TryGetValue(gateId, out effect))
                return;
            _enabledEffects.Remove(gateId);
            effect.Effect.Pause();
        }

        private void RemoveAllEffect()
        {
            foreach (var effect in _allEffects)
            {
                var rotatingParticle = effect.Value;
                rotatingParticle.Effect.Stop();
            }

            _allEffects.Clear();
            _enabledEffects.Clear();
        }


        public override void Draw()
        {
            if (MyAPIGateway.Multiplayer.IsServer) return;
            foreach (var effect in _enabledEffects)
            {
                var rotatingParticle = effect.Value;
                
                var angle = Vector3D.Rotate(
                    rotatingParticle.InitialMatrix.Translation - rotatingParticle.CenterMatrix.Translation,
                    MatrixD.CreateFromAxisAngle(rotatingParticle.CenterMatrix.Up,
                        rotatingParticle.CurrentAngle)); //Rotate the particle emitter every tick

                var finalPos = rotatingParticle.CenterMatrix.Translation +
                               Vector3D.Normalize(angle) * rotatingParticle.Radius;
                var finalUp = Vector3D.Normalize(rotatingParticle.CenterMatrix.Translation - finalPos);


                rotatingParticle.Effect.WorldMatrix =
                    MatrixD.CreateWorld(finalPos, finalUp, rotatingParticle.Effect.WorldMatrix.Up); //Final matrix
                rotatingParticle.CurrentAngle += 5f; //Increase the angle every tick

                //MyVisualScriptLogicProvider.CreateLightning(final_pos);
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Multiplayer.IsServer) return;
            RemoveAllEffect();
        }

        public class RotatingParticle
        {
            public MatrixD CenterMatrix; //Center position to orbit around
            public float CurrentAngle;
            public MyParticleEffect Effect;
            public MatrixD InitialMatrix; //Initial position
            public float Radius;

            public RotatingParticle(MyParticleEffect effect, float currentAngle, MatrixD initialMatrix,
                MatrixD centerMatrix, float radius)
            {
                Effect = effect;
                CurrentAngle = currentAngle;
                InitialMatrix = initialMatrix;
                CenterMatrix = centerMatrix;
                Radius = radius;
            }
        }
    }
}