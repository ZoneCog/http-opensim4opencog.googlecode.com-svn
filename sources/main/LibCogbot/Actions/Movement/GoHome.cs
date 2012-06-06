using System;
using MushDLR223.ScriptEngines;
using OpenMetaverse;

namespace Cogbot.Actions.Movement
{
    public class GoHomeCommand : Command, BotPersonalCommand
    {
        public GoHomeCommand(BotClient testClient)
        {
            Name = "gohome";
            Description = "Teleports home";
            Category = CommandCategory.Movement;
            Parameters = new[] { new NamedParam(typeof(GridClient), null) };
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            if (Client.Self.GoHome())
            {
                return Success("Teleport Home Succesful");
            }
            else
            {
                return Failure("Teleport Home Failed");
            }
        }
    }
}
