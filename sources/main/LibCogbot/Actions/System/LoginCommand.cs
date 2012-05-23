using System;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using Radegast;

namespace cogbot.Actions.System
{
    class Login : Command, BotSystemCommand
    {

        public Login(BotClient Client)
            : base(Client)
        {
            Name = "Login";
            Description = "Log into grid";
            Usage = Htmlize.Usage("login <first name> <last name> <password> [<loginuri>] [<location>]",
                "log into a grid");
            Category = CommandCategory.Security;
            Parameters = NamedParam.CreateParams(
                "first", typeof(string), "first name of bot",
                "last" , typeof(string), "last name of bot",
                "password", typeof(string), "password for bot",
                NamedParam.Optional("loginuri", typeof(Uri), "login uri for grid"),
                NamedParam.Optional("location", typeof(string), "one of home,last, or a sim name"));

        }

        public override CmdResult acceptInput(string verb, Parser args, OutputDelegate WriteLine)
        {
            //base.acceptInput(verb, args);
            string[] tokens = args.objectPhrase.Split(null);

            BotClient Client = TheBotClient;
            if (Client.IsLoggedInAndReady) return Success("Already logged in");
            //if ((tokens.Length != 1) && (tokens.Length != 3))
            //{
            //    return ("Please enter login FirstName LastName and Password to login to the SL");
            //}
            //else
            {
                if (tokens.Length > 0 && !String.IsNullOrEmpty(tokens[0]))
                {
                    Client.BotLoginParams.FirstName = tokens[0];
                }
                if (tokens.Length > 1)
                {
                    Client.BotLoginParams.LastName = tokens[1];
                }
                if (tokens.Length > 2)
                {
                    Client.BotLoginParams.Password = tokens[2];
                }
                if (tokens.Length > 3)
                {
                    Radegast.GridManager gm = new GridManager();
                    gm.LoadGrids();
                    string url = tokens[3];
                    string find = url.ToLower();
                    foreach (var grid in gm.Grids)
                    {
                        if (find == grid.Name.ToLower() || find == grid.ID.ToLower())
                        {
                            url = grid.LoginURI;
                        }
                    }
                    Client.BotLoginParams.URI = url;
                }
                if (tokens.Length > 4)
                {
                    Client.BotLoginParams.Start = tokens[4];
                }
                if (!Client.Network.Connected && !Client.Network.LoginMessage.StartsWith("Logging"))
                {
                    Client.Settings.LOGIN_SERVER = TheBotClient.BotLoginParams.URI;// ClientManager.SingleInstance.config.simURL; // "http://127.0.0.1:8002/";
                    ///                    Client.Network.Login(Client.BotLoginParams.FirstName, Client.BotLoginParams.LastName, Client.BotLoginParams.Password, "OnRez", "UNR");
                    WriteLine("$bot beginning login");
                    Client.Login();
                    WriteLine("$bot started login");
                }
                else
                    return Success("$bot is already logged in.");
            }
            return Success("loging in...");
        }
    }
}