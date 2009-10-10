using System;
using System.Collections.Generic;
using System.Globalization;
using OpenMetaverse;
using PathSystem3D.Navigation;
using cogbot.TheOpenSims;
using System.Windows.Forms;
using PathSystem3D.Navigation.Debug;
using System.Drawing;
using System.Net;

namespace cogbot.Actions.Movement
{
    class SMoveCommands
    {
    }
    //class ideal : cogbot.Actions.Command
    //{
    //    public ideal(BotClient client)
    //    {
    //        Name = GetType().Name;
    //        Description = "Starts the GUI debugger";
    //        Category = cogbot.Actions.CommandCategory.Movement;
    //    }

    //    public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
    //    {
    //        string[] tokens = args;
    //        if (tokens.Length > 0 && !String.IsNullOrEmpty(tokens[0]))
    //        {
    //            Client.BotLoginParams.FirstName = tokens[0];
    //        }
    //        if (tokens.Length > 1)
    //        {
    //            Client.BotLoginParams.LastName = tokens[1];
    //        }
    //        if (tokens.Length > 2)
    //        {
    //            Client.BotLoginParams.Password = tokens[2];
    //        }
    //        if (tokens.Length > 3)
    //        {
    //            Client.BotLoginParams.URI = tokens[3];
    //        }
    //        Thread th = new Thread(new ThreadStart(delegate()
    //        {

    //            try
    //            {
    //                tokens = new string[] { Client.BotLoginParams.FirstName, Client.BotLoginParams.LastName, Client.BotLoginParams.Password };
    //                METAboltInstance instance = new METAboltInstance(Client,true, tokens);              
    //                Application.Run(instance.MainForm);
    //            }
    //            catch (Exception e)
    //            {
    //                WriteLine("ideal error: " + e);
    //            }
    //        }));
    //        th.TrySetApartmentState(ApartmentState.STA);
    //        th.Start();
    //        ///  BaseIdealistViewer.guithread.Start();//.Main(args);
    //        return Success("Ran " + Name);
    //    }
    //}

    class srdebug : cogbot.Actions.Command, SystemApplicationCommand
    {
        public srdebug(BotClient client)
        {
            Name = GetType().Name;
            Description = "Starts the waypoint debuger";
            Category = cogbot.Actions.CommandCategory.Movement;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            GraphFormer gf = new GraphFormer(SimGlobalRoutes.Instance);
            gf.Show();
            return Success("Ran " + Name);
        }
    }
    class pfdebug : cogbot.Actions.Command, SystemApplicationCommand
    {
        public pfdebug(BotClient client)
        {
            Name = GetType().Name;
            Description = "Starts the pathfinder debuger";
            Category = cogbot.Actions.CommandCategory.Movement;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length == 0)
            {
                //foreach (SimRegion R in SimRegion.CurrentRegions)
                //{
                //    R.ShowDebugger();
                //}
                SimRegion.GetRegion(Client.Network.CurrentSim).ShowDebugger();
            }
            else
            {
                foreach (SimRegion R in SimRegion.CurrentRegions)
                {
                    if (R.RegionName.Contains(String.Join(" ", args)))
                        R.ShowDebugger();
                }
            }
            return Success("Ran " + Name);
        }
    }

    class pfcatchup : cogbot.Actions.Command
    {
        public pfcatchup(BotClient client)
        {
            Name = GetType().Name;
            Description = "Catches up the pathfinder";
            Category = cogbot.Actions.CommandCategory.Movement;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            lock (Client.Network.Simulators)
            {
                foreach (Simulator S in Client.Network.Simulators)
                {
                    WorldSystem.CatchUp(S);
                }
            }
            return Success("Ran " + Name);
        }
    }

    class pfg : cogbot.Actions.Command
    {
        public pfg(BotClient client)
        {
            Name = GetType().Name;
            Description = "pfg 180 5 will move backwards 5";
            Category = cogbot.Actions.CommandCategory.Movement;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            float Dist;
            if (args.Length > 1 && float.TryParse(args[1], out Dist))
            {
                Vector3d av = WorldSystem.TheSimAvatar.GetGlobalLeftPos(int.Parse(args[0]), Dist);
                WorldSystem.TheSimAvatar.MoveTo(av, 1f, 4);
            }
            else
            {
                Vector3d av = WorldSystem.TheSimAvatar.GetGlobalLeftPos(int.Parse(args[0]), 10);
                WorldSystem.TheSimAvatar.TurnToward(av);
            }
            return Success("Ran " + Name);
        }
    }

    class meshinfo : cogbot.Actions.Command
    {
        public meshinfo(BotClient client)
        {
            Name = GetType().Name;
            Description = "Reads the sim prims for improving routes";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length==0)
            {
                
            }
            IEnumerable<SimObject> objs = WorldSystem.GetAllSimObjects(String.Join(" ", args));
            foreach (SimObject o in objs)
            {
                WriteLine("MeshInfo: " + o);
                WriteLine(o.Mesh.DebugString());
            }
            return Success("Ran " + Name);
        }
    }

    class simzinfo : cogbot.Actions.Command
    {
        public simzinfo(BotClient client)
        {
            Name = GetType().Name;
            Description = "Calculates the Z level of walking at point. Usage: simzinfo 120 123";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimPosition),typeof(SimPosition)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            int argcount;
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            SimPathStore R = pos.PathStore;
            Vector3 v3 = pos.SimPosition;
            WriteLine("SimZInfo: " + pos + " " + R.GetGroundLevel(v3.X, v3.Y));
            SimWaypoint WP = R.GetWaypointOf(v3);
            WriteLine("WaypointInfo: {0}", WP.OccupiedString(R.GetCollisionPlane(v3.Z)));
            return Success("Ran " + Name);
        }
    }

    class simhinfo : cogbot.Actions.Command
    {
        public simhinfo(BotClient client)
        {
            Name = GetType().Name;
            Description = "Calculates the Height (Z) level of walking at point. Usage: simzinfo 120 123 30";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimPosition),typeof(SimPosition)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            int argcount;
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            SimPathStore R = pos.PathStore;
            Vector3 v3 = pos.SimPosition;
            WriteLine("SimZInfo: " + pos + " " + R.GetGroundLevel(v3.X, v3.Y));

#if COLLIDER_ODE  
            Vector3 landing = R.CreateAndDropPhysicalCube(v3);
            WriteLine("SimHInfo: {0}", landing);
#endif
            return Success("Ran " + Name);
        }
    }



    class srmap : cogbot.Actions.Command
    {
        public srmap(BotClient client)
        {
            Name = GetType().Name;
            Description = "Reads the sim map for improving routes";
            Category = cogbot.Actions.CommandCategory.Movement;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            Image I = null;// WorldSystem.miniMap.Image;
            if (I == null)
            {

                String picUri = "http://71.197.210.170:9000/index.php?method=regionImaged63a88fe7db448c6b1a52b7628fe8d0d";
                // Create the requests.
                WebRequest requestPic = WebRequest.Create(picUri);

                WebResponse responsePic = requestPic.GetResponse();

                I = Image.FromStream(responsePic.GetResponseStream());

            }

            WorldSystem.SimPaths.UpdateFromImage(I);
            return Success("Ran " + Name);
        }
    }
    class srprim : cogbot.Actions.Command
    {
        public srprim(BotClient client)
        {
            Name = GetType().Name;
            Description = "Reads the sim prims for improving routes then bakes the region";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            IEnumerable<SimObject> objs = WorldSystem.GetAllSimObjects(String.Join(" ", args));
            foreach (SimObject o in objs)
            {
                o.UpdateOccupied();
            }
            SimRegion.BakeRegions();
            return Success("Ran " + Name);
        }
    }

    //class srpath : cogbot.Actions.Command
    //{
    //    public srpath(BotClient client)
    //    {
    //        Name = GetType().Name;
    //        Description = "Show the route to the object";
    //        Category = cogbot.Actions.CommandCategory.Movement;
    //    }

    //    public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
    //    {
    //        int argsused;
    //        SimPosition v3 = WorldSystem.GetVector(args, out argsused);
    //        CollisionIndex wp = v3.GetWaypoint();
    //        bool IsFake;
    //        IList<SimRoute> route = WorldSystem.TheSimAvatar.GetRouteList(wp, out IsFake);
    //        String s = "v3=" + WorldSystem.TheSimAvatar.DistanceVectorString(v3) + " wp=" + wp.ToString();
    //        if (IsFake)
    //        {
    //            s += "\nIsFake: ";
    //        }
    //        else
    //        {
    //            s += "\nComputed ";
    //        }
    //        if (route!=null)
    //        for (int i = 0; i < route.Count; i++)
    //        {
    //            s += " \n" + i + ": " + route[i].ToInfoString();
    //        }
    //        return s;
    //    }
    //}


    class srm : cogbot.Actions.Command
    {
        public srm(BotClient client)
        {
            Name = GetType().Name;
            Description = "Move to a the specified point using MoveTo";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimPosition), typeof(SimPosition)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            int argcount;
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            if (pos == null)
            {
                return Failure("Cannot " + Name + " to " + String.Join(" ", args));
            }
            int maxSeconds = 6;
            float maxDistance = 1f;
            if (argcount < args.Length)
            {
            }
            string str = "MoveTo(" + pos.SimPosition + ", " + maxDistance + ", " + maxSeconds + ")";
            WriteLine("Starting  " + str);
            bool MadIt = WorldSystem.TheSimAvatar.MoveTo(pos.GlobalPosition, maxDistance, maxSeconds);
            if (MadIt)
            {
                return Success("SUCCESS " + str);

            }
            else
            {
                return Success("FAILED " + str);
            }
        }
    }

    class srg : cogbot.Actions.Command
    {
        public srg(BotClient client)
        {
            Name = GetType().Name;
            Description = "Use A* Pathfinding to get to object";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] { new NamedParam( typeof(SimPosition), typeof(Vector3d)) };

        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            int argcount;
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            if (pos == null)
            {
                return Failure(String.Format("Cannot {0} to {1}", Name, String.Join(" ", args)));
            }
            int maxSeconds = 6;
            float maxDistance = 1f;
            if (argcount < args.Length)
            {
            }
            String str = "GotoTarget(" + pos + ")";
            WriteLine(str);
            bool MadIt = WorldSystem.TheSimAvatar.GotoTarget(pos);
            if (MadIt)
            {
                return Success(string.Format("SUCCESS {0}", str));

            }
            else
            {
                return Success("FAILED " + str);
            }
        }
    }

    class blocktw : cogbot.Actions.Command
    {
        public blocktw(BotClient client)
        {
            Name = GetType().Name;
            Description = "Puts one minute temp blocks toward objects";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimPosition), typeof(SimPosition)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            int argcount;
            SimPosition pos = WorldSystem.GetVector(args, out argcount);
            if (pos == null)
            {
                return Failure("Cannot " + Name + " to " + String.Join(" ", args));
            }

            Vector3d v3d = pos.GlobalPosition;
            Vector3 v3 = pos.SimPosition;
            SimAbstractMover sam = SimCollisionPlaneMover.CreateSimPathMover(WorldSystem.TheSimAvatar, pos, pos.GetSizeDistance());
            sam.BlockTowardsVector(v3);
            return Success("SUCCESS ");
        }
    }

    class gto : cogbot.Actions.Command
    {
        public gto(BotClient client)
        {
            Name = "gto";
            Description = "Go to the avatar toward the specified position for a maximum of seconds. gto [prim | [x y]] [dist]";
            Category = cogbot.Actions.CommandCategory.Movement;
            Parameters = new[] {  new NamedParam(typeof(SimPosition), typeof(SimPosition)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            float distance = 2.0f;

            int argsUsed;
            SimPosition simObject = WorldSystem.GetVector(args, out argsUsed);

            if (simObject==null) return Failure("Cannot find " + string.Join(" ", args)); 
            if (!simObject.IsRegionAttached)
            {
                return Failure("Cannot get SimPosition of " + simObject);
            }

            distance = 0.5f + simObject.GetSizeDistance();
            if (argsUsed < args.Length)
            {
                float d;
                if (float.TryParse(args[argsUsed], out d))
                {
                    distance = d;
                }
            }
            WriteLine("gto {0} {1}", simObject, distance);
            WorldSystem.TheSimAvatar.MoveTo(simObject.GlobalPosition, distance, 10);
            WorldSystem.TheSimAvatar.StopMoving();
            return Success(WorldSystem.TheSimAvatar.DistanceVectorString(simObject));
        }

        private void Goto(Vector3 target, float p)
        {

            if (true)
            {
                uint x, y;
                Utils.LongToUInts(Client.Network.CurrentSim.Handle, out x, out y);
                Vector2 v2 = new Vector2(target.X, target.Y);
                Vector2 cp = new Vector2(GetSimPosition().X, GetSimPosition().Y);
                float d = Vector2.Distance(v2, cp);
                float dl = d;
                bool autoOff = false;
                while (d > p)
                {
                    if (autoOff)
                    {
                        Client.Self.Movement.TurnToward(target);
                        Client.Self.AutoPilot((ulong)(x + target.X), (ulong)(y + target.Y), GetSimPosition().Z);
                        autoOff = false;
                    }
                    cp = new Vector2(GetSimPosition().X, GetSimPosition().Y);
                    d = Vector2.Distance(v2, cp);
                    if (dl < d)
                    {
                        Client.Self.AutoPilotCancel();
                        autoOff = true;
                        Client.Self.Movement.TurnToward(target);
                        Client.Self.Movement.Stop = true;
                        Client.Self.Movement.AtPos = false;
                        Client.Self.Movement.NudgeAtPos = true;
                        Client.Self.Movement.SendUpdate(true);
                        Client.Self.Movement.NudgeAtPos = false;
                        Client.Self.Movement.SendUpdate(true);
                    }
                    //Thread.Sleep(10);
                    Application.DoEvents();
                    dl = d;
                }
                Client.Self.Movement.TurnToward(target);
                Client.Self.AutoPilotCancel();
                return;
            }
        }
    }



}
