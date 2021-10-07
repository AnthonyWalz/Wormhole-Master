using System.Collections.Generic;
using System.Linq;
using System.Text;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Wormhole
{
    public class HelpCommand : CommandModule
    {
        [Command("wormhole", "Wormhole commands list.", null)]
        [Permission(MyPromoteLevel.None)]
        public void Wormholehelp()
        {
            CommandManager commandManager = Context.Torch.CurrentSession?.Managers.GetManager<CommandManager>();
            if (commandManager == null)
                return;

            commandManager.Commands.GetNode(new List<string> { "wormhole" }, out var commandNode);
            if (commandNode != null)
            {
                var stringBuilder = new StringBuilder();
                if (commandNode.Command != null)
                {
                    stringBuilder.AppendLine("Commands syntax: " + commandNode.Command.SyntaxHelp);
                    stringBuilder.Append(commandNode.Command.HelpText);
                }

                if (commandNode.Subcommands.Count() != 0)
                {
                    _ = stringBuilder.Append("\nSubcommands: " + string.Join(", ", from e in commandNode.Subcommands
                                                                                   where Context.Player == null || e.Value.Command?.MinimumPromoteLevel <= Context.Player.PromoteLevel
                                                                                   select e into x
                                                                                   select x.Key));
                }

                Context.Respond(stringBuilder.ToString());
            }
        }
    }
}