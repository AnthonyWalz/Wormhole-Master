using System;
using System.Reflection;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Utils;

namespace Wormhole
{
    [PatchShim]
    public static class CharacterPatch
    {
        [ReflectedGetter(Name = "m_savedPilot")]
        private static readonly Func<MyCockpit, MyCharacter> _savedPilotGetter = null!;
        
        public static void Patch(PatchContext context)
        {
            context.GetPattern(typeof(MyCockpit).GetMethod("Init",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).Suffixes
                .Add(typeof(CharacterPatch).GetMethod(nameof(Postfix)));
        }

        public static void Postfix(MyCockpit __instance)
        {
            var pilot = _savedPilotGetter(__instance);
            var identity = pilot?.GetIdentity();
            if (identity is null) return;
            if (identity.Character == pilot) return;

            if (identity.Character is { })
            {
                if (identity.Character.IsUsing is MyCockpit cockpit)
                    cockpit.RemovePilot();
                identity.Character.Close();
            }
            
            identity.ChangeCharacter(pilot);
            __instance.AttachPilot(pilot);
            
            if (Sync.Players.TryGetPlayerId(identity.IdentityId, out var id) && Sync.Players.TryGetPlayerById(id, out var player))
                Sync.Players.SetPlayerCharacter(player, pilot, __instance);
        }
    }
}