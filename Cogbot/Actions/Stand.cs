using System;
using System.Collections.Generic;
using System.Text;
using cogbot.TheOpenSims;
using OpenMetaverse; //using libsecondlife;

using MushDLR223.ScriptEngines;

namespace cogbot.Actions.Movement
{
    class Stand : Command, BotPersonalCommand
    {
        public Stand(BotClient Client)
            : base(Client)
        {
            Description = "Stand up.";
            Usage = "To Stand up, type \"stand\"";
        }

        public override CmdResult acceptInput(string verb, Parser args, OutputDelegate WriteLine)
        {
            //base.acceptInput(verb, args);

            SimActor sitter = WorldSystem.TheSimAvatar;
            if (!sitter.IsSitting)
            {
                return Success("$bot is already standing.");
            }
            else
            {
                sitter.StandUp();
            }

            Client.describeNext = true;
            return Success("$bot stood up.");
        }
    }
}
