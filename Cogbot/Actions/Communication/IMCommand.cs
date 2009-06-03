using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace cogbot.Actions
{
    public class ImCommand : Command
    {
        string ToAvatarName = String.Empty;
        ManualResetEvent NameSearchEvent = new ManualResetEvent(false);
        Dictionary<string, UUID> Name2Key = new Dictionary<string, UUID>();
        AvatarManager.AvatarNameSearchCallback callback;

        public ImCommand(BotClient testClient)
        {
            callback = new AvatarManager.AvatarNameSearchCallback(Avatars_OnAvatarNameSearch);

            Name = "im";
            Description = "Instant message someone. Usage: im [firstname] [lastname] [message]";
            Category = CommandCategory.Communication;
        }

        public override string Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length < 3)
                return "Usage: im [firstname] [lastname] [message]";

            ToAvatarName = args[0] + " " + args[1];

            // Build the message
            string message = String.Empty;
            for (int ct = 2; ct < args.Length; ct++)
                message += args[ct] + " ";
            message = message.TrimEnd();
            try
            {
                Client.Avatars.OnAvatarNameSearch += callback;
                if (message.Length > 1023) message = message.Remove(1023);

                if (!Name2Key.ContainsKey(ToAvatarName.ToLower()))
                {
                    // Send the Query
                    Client.Avatars.RequestAvatarNameSearch(ToAvatarName, UUID.Random());

                    NameSearchEvent.WaitOne(6000, false);
                }

                if (Name2Key.ContainsKey(ToAvatarName.ToLower()))
                {
                    UUID id = Name2Key[ToAvatarName.ToLower()];

                    Client.Self.InstantMessage(id, message);
                    return "Instant Messaged " + id.ToString() + " with message: " + message;
                }
                else
                {
                    return "Name lookup for " + ToAvatarName + " failed";
                }
            }
            finally
            {
                Client.Avatars.OnAvatarNameSearch -= callback;
            }
        }

        void Avatars_OnAvatarNameSearch(UUID queryID, Dictionary<UUID, string> avatars)
        {
            foreach (KeyValuePair<UUID, string> kvp in avatars)
            {
                if (kvp.Value.ToLower() == ToAvatarName.ToLower())
                {
                    Name2Key[ToAvatarName.ToLower()] = kvp.Key;
                    NameSearchEvent.Set();
                    return;
                }
            }
        }
    }
}
