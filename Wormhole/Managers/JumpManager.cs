using System;
using System.Threading.Tasks;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.API;
using Torch.Managers;
using Torch.Utils;
using Wormhole.ViewModels;

namespace Wormhole.Managers
{
    public class JumpManager : Manager
    {
        [Dependency] private readonly ClientEffectsManager _effectsManager = null!;

        public JumpManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public async Task StartJump(GateViewModel gateViewModel, MyPlayer player, MyCubeGrid grid)
        {
            // TODO hyper jump effect
            MyVisualScriptLogicProvider.ShowNotification("Opening the gate...",
                (int) TimeSpan.FromSeconds(4).TotalMilliseconds, playerId: player.Identity.IdentityId);
            _effectsManager.NotifyJumpStatusChanged(JumpStatus.Started, gateViewModel, grid);
            await Task.Delay(TimeSpan.FromSeconds(12));
            _effectsManager.NotifyJumpStatusChanged(JumpStatus.Ready, gateViewModel, grid);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}