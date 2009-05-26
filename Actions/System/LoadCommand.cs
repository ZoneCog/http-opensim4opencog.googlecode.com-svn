using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace cogbot.Actions
{
    public class LoadCommand : Command, BotSystemCommand
    {
        public LoadCommand(BotClient testClient)
		{
			Name = "load";
			Description = "Loads commands from a dll. (Usage: load AssemblyNameWithoutExtension)";
            Category = CommandCategory.TestClient;
		}

		public override string Execute(string[] args, UUID fromAgentID)
		{
			if (args.Length < 1)
				return "Usage: load AssemblyNameWithoutExtension";

            BotClient Client = TheBotClient;

			string filename = AppDomain.CurrentDomain.BaseDirectory + args[0] + ".dll";
			Client.RegisterAllCommands(Assembly.LoadFile(filename));
            return "Assembly " + filename + " loaded.";
		}
    }
}
