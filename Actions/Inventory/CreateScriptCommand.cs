using System;
using System.IO;
using OpenMetaverse;

namespace cogbot.Actions
{
    public class CreateScriptCommand : Command
    {
        public CreateScriptCommand(BotClient testClient)
        {
            Name = "createscript";
            Description = "Creates a script from a local text file.";
            Category = CommandCategory.Inventory;
        }

        void OnNoteUpdate(bool success, string status, UUID itemID, UUID assetID)
        {
            if (success)
                WriteLine("Script successfully uploaded, ItemID {0} AssetID {1}", itemID, assetID);
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: createscript filename.txt";

            string file = String.Empty;
            for (int ct = 0; ct < args.Length; ct++)
                file = file + args[ct] + " ";
            file = file.TrimEnd();

            WriteLine("Filename: {0}", file);
            if (!File.Exists(file))
                return String.Format("Filename '{0}' does not exist", file);

            System.IO.StreamReader reader = new StreamReader(file);
            string body = reader.ReadToEnd();

            // FIXME: Upload the script asset first. When that completes, call RequestCreateItem
            try
            {
                string desc = String.Format("{0} created by OpenMetaverse BotClient {1}", file, DateTime.Now);
                // create the asset

                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.LSLText),
                    file, desc, AssetType.LSLText, UUID.Random(), InventoryType.LSL, PermissionMask.All,
                    delegate(bool success, InventoryItem item)
                    {
                        if (success) // upload the asset
                            Client.Inventory.RequestUpdateScriptAgentInventory(CreateScriptAsset(body), item.UUID, new InventoryManager.ScriptUpdatedCallback(OnNoteUpdate));
                    }
                );
                return "Done";

            }
            catch (System.Exception e)
            {
                Logger.Log(e.ToString(), Helpers.LogLevel.Error, Client);
                return "Error creating script.";
            }
        }
        /// <summary>
        /// </summary>
        /// <param name="body"></param>
        public static byte[] CreateScriptAsset(string body)
        {
            // Format the string body into Linden text
            string lindenText = "";/* "Linden text version 1\n";
            lindenText += "{\n";
            lindenText += "LLEmbeddedItems version 1\n";
            lindenText += "{\n";
            lindenText += "count 0\n";
            lindenText += "}\n";
            lindenText += "Text length " + body.Length + "\n";*/
            lindenText += body;
            //lindenText += "}\n";

            // Assume this is a string, add 1 for the null terminator
            byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(lindenText);
            byte[] assetData = new byte[stringBytes.Length]; //+ 1];
            Array.Copy(stringBytes, 0, assetData, 0, stringBytes.Length);

            return assetData;
        }
    }
}