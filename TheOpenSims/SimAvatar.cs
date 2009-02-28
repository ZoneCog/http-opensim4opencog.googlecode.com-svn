using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using DotLisp;
using System.Reflection;
using cogbot.Listeners;
using System.Threading;
using System.Windows.Forms;
using cogbot.TheOpenSims.Navigation;
using System.Collections;
using System.Drawing;
//Complex outcomes may be a result of simple causes, or they may just be complex by nature. 
//Those complexities that turn out to have simple causes can be simulated and studied, 
//thus increasing our knowledge without needing direct observation.
namespace cogbot.TheOpenSims
{

    public class SimAvatar : SimObject, SimMover
    {

        public Thread avatarThinkerThread = null;
        public Thread avatarHeartbeatThread = null;

        public Avatar theAvatar
        {
            get { return (Avatar)thePrim; }
        }

        public SimAvatar InDialogWith = null;

        readonly public BotNeeds CurrentNeeds;
        public float SightRange = 30.0f;


        // things the bot cycles through mentally
        public ListAsSet<SimObject> KnownSimObjects = new ListAsSet<SimObject>();

        public List<SimObject> GetKnownObjects()
        {
            ScanNewObjects(3, SightRange);
            SortByDistance(KnownSimObjects);
            return KnownSimObjects;
        }

        // which will result in 
        public List<BotAction> KnownBotActions = new List<BotAction>();

        // which will be skewed with how much one bot like a Mental Aspect
        public Dictionary<BotMentalAspect, int> AspectEnjoyment = new Dictionary<BotMentalAspect, int>();

        //notice this also stores object types that pleases the bot as well as people
        // (so how much one bot likes another avatar is sotred here as well)

        // Actions tbe bot might do next cycle.
        List<BotAction> TodoBotActions = new List<BotAction>();

        // Actions observed
        List<BotAction> ObservedBotActions = new List<BotAction>();

        // Action template stubs 
        List<SimTypeUsage> KnownTypeUsages = new List<SimTypeUsage>();


        // assuptions about stubs
        public Dictionary<SimObjectType, BotNeeds> Assumptions = new Dictionary<SimObjectType, BotNeeds>();

        // Current action 
        public BotAction CurrentAction = null;



        public SimAvatar(Avatar slAvatar, WorldObjects objectSystem)
            : base(slAvatar, objectSystem)
        {
            WorldObjects.SimAvatars.Add(this);
            ObjectType.SuperType.Add(SimTypeSystem.GetObjectType("Avatar"));
            CurrentNeeds = new BotNeeds(90.0F);
            try
            {

                AspectName = slAvatar.Name;
            }
            catch (Exception e)
            {
                AspectName += objectSystem.client + "_Avatar_" + slAvatar.LocalID;
            }
            avatarHeartbeatThread = new Thread(new ThreadStart(Aging));
            avatarHeartbeatThread.Name = "AvatarHeartbeatThread for " + AspectName;
            avatarHeartbeatThread.Start();
            ApproachThread = new Thread(TrackerLoop);
            ApproachThread.Name = "TrackerLoop for " + AspectName;
            MakeEnterable(this);
        }

        public override bool RestoreEnterable(SimAvatar agent)
        {
            return false;// base.RestoreEnterable(this);
        }

        public override bool IsRoot()
        {
            return true;
        }
        public override SimObject Parent {  get { return this; }   }

        public bool IsSitting()
        {
            //BotClient Client = base.WorldSystem.client;
            if (IsLocal())
            {
                AgentManager ClientSelf = Client.Self;
                AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
                if (ClientMovement.SitOnGround) return true;
                return ClientSelf.SittingOn != 0;
            }
            return theAvatar.ParentID != 0;
        }

        public bool IsLocal()
        {
            if (Client == null) return false;
            AgentManager ClientSelf = Client.Self;
            return ClientSelf.AgentID == theAvatar.ID || ClientSelf.LocalID == theAvatar.LocalID;
        }

        public override void UpdatePaths(SimPathStore simPathStore)
        {
        }

        public override string DebugInfo()
        {
            String s = ToString();
            List<SimObject> KnowsAboutList = GetKnownObjects();
            KnowsAboutList.Sort(CompareObjects);
            int show = 10;
            s += "\nKnowsAboutList: " + KnowsAboutList.Count;
            foreach (SimObject item in KnowsAboutList)
            {
                show--;
                if (show < 0) break;
                //if (item is SimAvatar) continue;
                s += "\n   " + item + " " + DistanceVectorString(item);
            }
            show = 10;
            KnownTypeUsages.Sort(CompareUsage);
            s += "\nKnownTypeUsages: " + KnownTypeUsages.Count;
            foreach (SimTypeUsage item in KnownTypeUsages)
            {
                show--;
                if (show < 0) break;
                //if (item is SimAvatar) continue;
                s += "\n   " + item + " " + item.RateIt(CurrentNeeds);
            }
            return "\n" + s;
        }

        public void StartThinking()
        {
            if (avatarThinkerThread == null)
            {
                avatarThinkerThread = new Thread(new ThreadStart(Think));
                avatarThinkerThread.Name = "AvatarThinkerThread for " + Client;
                if (IsLocal())
                {
                    // only think for ourselves
                    avatarThinkerThread.Start();
                }
            }
            if (!avatarThinkerThread.IsAlive) avatarThinkerThread.Resume();
        }

        public bool IsThinking()
        {
            return (avatarThinkerThread != null);
        }
        public void PauseThinking()
        {
            if (avatarThinkerThread != null)
            {
                try
                {
                    // avatarThinkerThread.Suspend();
                    avatarThinkerThread.Abort();
                    avatarThinkerThread = null;
                }
                catch (Exception)
                {
                }
            }
        }

        public override Vector3 GetSimPosition()
        {
            if (IsLocal()) return Client.Self.SimPosition;
            return base.GetSimPosition();
        }

        public override Quaternion GetSimRotation()
        {
            if (IsLocal()) return Client.Self.SimRotation;
            return base.GetSimRotation();
        }

        public void Think()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);
                    ThinkOnce();
                }
                catch (Exception e)
                {
                    Debug(e.ToString());
                }
            }
        }
        public void Aging()
        {
            while (true)
            {
                CurrentNeeds.AddFrom(SimTypeSystem.GetObjectType("OnMinuteTimer").GetUsageActual("OnMinuteTimer"));
                CurrentNeeds.SetRange(0.0F, 100.0F);
                Thread.Sleep(60000); // one minute
                // Debug(CurrentNeeds.ToString());
            }
        }


        public void ThinkOnce()
        {
            ScanNewObjects(2, SightRange);

            Thread.Sleep(2000);
            CurrentAction = GetNextAction();
            if (CurrentAction != null)
            {
                UseAspect(CurrentAction);
            }
        }

        public BotAction GetNextAction()
        {
            BotAction act = CurrentAction;

            IList<BotAction> acts = GetPossibleActions();

            if (acts.Count > 0)
            {
                act = (BotAction)FindBestUsage(acts);
                acts.Remove(act);
            }
            return act;
        }

        public SimUsage FindBestUsage(IEnumerable acts)
        {
            SimUsage bestAct = null;
            if (acts != null)
            {
                IEnumerator enumer = acts.GetEnumerator();
                float bestRate = float.MinValue;
                while (enumer.MoveNext())
                {
                    SimUsage b = (SimUsage)enumer.Current;
                    float brate = b.RateIt(CurrentNeeds);
                    if (brate > bestRate)
                    {
                        bestAct = b;
                        bestRate = brate;
                    }
                }
            }
            return bestAct;
        }

        //public void AddGrass(Simulator simulator, Vector3 scale, Quaternion rotation, Vector3 position, Grass grassType, UUID groupOwner)
        //{
        //}
        //public void AddPrim(Simulator simulator, Primitive.ConstructionData prim, UUID groupID, Vector3 position, Vector3 scale, Quaternion rotation)
        //{
        //}
        //public void AddTree(Simulator simulator, Vector3 scale, Quaternion rotation, Vector3 position, Tree treeType, UUID groupOwner, bool newTree)
        //{
        //}
        //public void AttachObject(Simulator simulator, uint localID, AttachmentPoint attachPoint, Quaternion rotation)
        //{
        //}

        //public static Primitive.ConstructionData BuildBasicShape(PrimType type)
        //{
        //}

        //public SimObject RezObjectType(SimObject copyOf)
        //{
        //    string treeName = args[0].Trim(new char[] { ' ' });
        //    Tree tree = (Tree)Enum.Parse(typeof(Tree), treeName);

        //    Vector3 treePosition = ClientSelf.SimPosition;
        //    treePosition.Z += 3.0f;

        //    Client.Objects.AddTree(Client.Network.CurrentSim, new Vector3(0.5f, 0.5f, 0.5f),
        //        Quaternion.Identity, treePosition, tree, Client.GroupID, false);

        //    //ClientSelf.
        //    return copyOf;
        //}

        //public void SortActs(List<SimUsage> acts)
        //{
        //    acts.Sort(CompareUsage);
        //}

        public int CompareUsage(SimUsage act1, SimUsage act2)
        {
            return (int)(act2.RateIt(CurrentNeeds) - act1.RateIt(CurrentNeeds));
        }

        public int CompareObjects(SimObject act1, SimObject act2)
        {
            return (int)(act2.RateIt(CurrentNeeds) - act1.RateIt(CurrentNeeds));
        }

        public IList<BotAction> GetPossibleActions()
        {
            if (TodoBotActions.Count < 2)
            {
                TodoBotActions = NewPossibleActions();
            }
            return TodoBotActions;
        }

        public List<BotAction> NewPossibleActions()
        {
            List<SimObject> knowns = GetKnownObjects();
            List<BotAction> acts = new List<BotAction>();
            foreach (BotAction obj in ObservedBotActions)
            {
                acts.Add(obj);
            }

            foreach (SimObject obj in knowns)
            {
                foreach (SimObjectUsage objuse in obj.GetUsages())
                {
                    acts.Add(new BotObjectAction(this, objuse));
                    foreach (SimTypeUsage puse in KnownTypeUsages)
                    {
                        //acts.Add( new BotObjectAction(this, puse, obj));
                    }

                }
            }
            return acts;
        }

        public void DoBestUse(SimObject someObject)
        {
            SimTypeUsage use = someObject.GetBestUse(CurrentNeeds);
            if (use == null)
            {
                float closeness = Approach(someObject, 2);
                AgentManager ClientSelf = Client.Self;
                ClientSelf.Touch(someObject.thePrim.LocalID);
                if (closeness < 3)
                {
                    ClientSelf.RequestSit(someObject.thePrim.ID, Vector3.Zero);
                    ClientSelf.Sit();
                }
                return;
            }
            UseAspect(new BotObjectAction(this, new SimObjectUsage(use, someObject)));
            return;
        }


        public void UseAspect(BotMentalAspect someAspect)
        {
            if (someAspect is BotAction)
            {
                BotAction act = (BotAction)someAspect;
                act.InvokeReal();
                return;
            }
            if (InDialogWith != null)
            {
                TalkTo(InDialogWith, someAspect);
                return;
            }

            if (someAspect is SimObject)
            {
                SimObject someObject = (SimObject)someAspect;
                DoBestUse(someObject);
            }

        }

        List<SimObject> InterestingObjects = new List<SimObject>();

        public SimObject GetNextInterestingObject()
        {
            SimObject mostInteresting = null;
            if (InterestingObjects.Count < 2)
            {
                InterestingObjects = GetKnownObjects();
                InterestingObjects.Remove(this);
            }
            int count = InterestingObjects.Count - 2;
            foreach (BotMentalAspect cAspect in InterestingObjects)
            {
                if (cAspect is SimObject)
                {
                    if (mostInteresting == null)
                    {
                        mostInteresting = (SimObject)cAspect;
                        // break;
                    }
                    else
                    {
                        mostInteresting = (SimObject)CompareTwo(mostInteresting, cAspect);
                    }
                    count--;
                    if (count < 0) break;
                }
            }
            InterestingObjects.Remove(mostInteresting);
            InterestingObjects.Add(mostInteresting);
            return mostInteresting;
        }

        readonly Random MyRandom = new Random(DateTime.Now.Millisecond);
        // TODO Real Eval routine
        public BotMentalAspect CompareTwo(BotMentalAspect mostInteresting, BotMentalAspect cAspect)
        {
            if ((mostInteresting is SimObject) && (cAspect is SimObject))
            {
                int rate = CompareObjects((SimObject)mostInteresting, (SimObject)cAspect);
                if (rate > 0) return cAspect;
                if (rate < 0) return mostInteresting;
            }
            return (MyRandom.Next(1, 2) == 1) ? mostInteresting : cAspect;
        }

        public void ScanNewObjects(int minimum, float sightRange)
        {
            List<SimObject> objects = GetNearByObjects(sightRange, true);
            lock (objects)
            {
                foreach (SimObject obj in objects)
                {
                    if (obj != this)
                        if (obj.IsRoot() || obj.IsTyped())
                        {
                            lock (KnownSimObjects) if (!KnownSimObjects.Contains(obj))
                            {
                                KnownSimObjects.Add(obj);
                                IList<SimTypeUsage> uses = obj.GetTypeUsages();
                                foreach (SimTypeUsage use in uses)
                                {
                                    lock (KnownTypeUsages) if (!KnownTypeUsages.Contains(use))
                                    {
                                        KnownTypeUsages.Add(use);
                                    }
                                }
                            }
                        }
                }
            }
            if (KnownSimObjects.Count < minimum)
            {
                if (sightRange < 255)
                    ScanNewObjects(minimum, sightRange + 10);
            }
        }

        // Avatars approach distance
        public override float GetSizeDistance()
        {
            return 2f;
        }


        public BotClient GetGridClient()
        {
            //if (Client != null) return Client;
            //BotClient Client = WorldSystem.client;
            //if (theAvatar.ID != ClientSelf.AgentID)
            //{
            //    throw new Exception("This avatar " + theAvatar + " has no GridClient");
            //}
            return Client;
        }

        public void TalkTo(SimAvatar avatar, String talkAbout)
        {
            SimAvatar avatarWasInDialogWith = avatar.InDialogWith;
            SimAvatar wasInDialogWith = InDialogWith;
            try
            {
                InDialogWith = avatar;
                BotClient Client = GetGridClient();
                AgentManager ClientSelf = Client.Self;
                AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
                TurnToward(InDialogWith);
                ClientSelf.AnimationStop(Animations.TALK, true);
                ClientSelf.AnimationStart(Animations.TALK, true);
                Client.Talk(InDialogWith + ": " + talkAbout);
                Thread.Sleep(3000);
                ClientSelf.AnimationStop(Animations.TALK, true);
            }
            finally
            {
                InDialogWith = wasInDialogWith;
                avatar.InDialogWith = avatarWasInDialogWith;
            }
        }

        public void TalkTo(SimAvatar avatar, BotMentalAspect talkAbout)
        {
            // TODO find a better text represantation (a thought bubble maybe?)
            TalkTo(avatar, "" + talkAbout);
        }

        public override void Debug(string p, params object[] args)
        {
            WorldSystem.output(String.Format(p,args));
        }

        public void Eat(SimObject target)
        {
            Debug("!!! EAT " + target);
        }

        public ThreadStart WithSitOn(SimObject obj, ThreadStart closure)
        {
            BotClient Client = GetGridClient();
            AgentManager ClientSelf = Client.Self;
            return new ThreadStart(delegate()
            {
                Primitive targetPrim = obj.thePrim;
               // ClientSelf.RequestSit(targetPrim.ID, Vector3.Zero);
                //ClientSelf.Sit();
                try
                {
                    closure.Invoke();
                }
                finally
                {
                    //ClientSelf.Stand();
                }
            });
        }

        public ThreadStart WithGrabAt(SimObject obj, ThreadStart closure)
        {
            BotClient Client = GetGridClient();
            return new ThreadStart(delegate()
            {
                Primitive targetPrim = obj.thePrim;
                uint objectLocalID = targetPrim.LocalID;
                AgentManager ClientSelf = Client.Self;
                try
                {
                    ClientSelf.Grab(objectLocalID);
                    closure.Invoke();
                }
                finally
                {
                    ClientSelf.DeGrab(objectLocalID);
                }
            });
        }

        public ThreadStart WithAnim(UUID anim, ThreadStart closure)
        {
            BotClient Client = GetGridClient();
            AnimThread animThread = new AnimThread(Client.Self, anim);
            return new ThreadStart(delegate()
            {
                try
                {
                    animThread.Start();
                    closure.Invoke();
                }
                finally
                {
                    animThread.Stop();
                }
            });
        }

        public UUID FindAnimUUID(string use)
        {
            return cogbot.TheOpenSims.SimAnimation.GetAnimationUUID(use);
        }

        public void ExecuteLisp(SimObjectUsage botObjectAction, String lisp)
        {
            BotClient Client = GetGridClient();
            if (!String.IsNullOrEmpty(lisp))
            {
                Client.lispTaskInterperter.Intern("TheBot", this);
                Client.lispTaskInterperter.Intern("Target", botObjectAction.Target);
                Client.lispTaskInterperter.Intern("botObjectAction", botObjectAction);
                Client.evalLispString((String)lisp);
            }
        }


        public override bool IsFloating
        {
            get
            {
                AgentManager ClientSelf = Client.Self;
                AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
                return ClientMovement.Fly;
            }
            set
            {
                if (IsFloating != value)
                {
                    AgentManager ClientSelf = Client.Self;
                    ClientSelf.Fly(value);
                }
            }
        }

        public override string GetName()
        {
            try
            {
                return theAvatar.Name;
            }
            catch (Exception)
            {
                return AspectName;
            }
        }

        public override string ToString()
        {
            return GetName();
        }

        BotClient Client;
        public void SetClient(BotClient Client)
        {
            this.Client = Client;
            WorldSystem = Client.WorldSystem;
            if (IsLocal())
            {
                WorldSystem.SetSimAvatar(this);
                if (ApproachThread != null)
                {
                    if (!ApproachThread.IsAlive)                    
                        ApproachThread.Start();
                }
            }
            //WorldSystem.AddTracking(this,Client);
        }

        public SimObject FindSimObject(SimObjectType pUse)
        {
            IList<SimObject> objects = GetKnownObjects();
            foreach (SimObject obj in objects)
            {
                if (obj.IsTypeOf(pUse) != null) return obj;
            }
            return null;
        }

        public override bool Matches(string name)
        {
            return SimTypeSystem.MatchString(base.ToString(), name)
                || SimTypeSystem.MatchString(ToString(), name);
        }

        public SimObject StandUp()
        {
            SimObject UnPhantom = null;
            AgentManager ClientSelf = Client.Self;
            AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
            if (ClientMovement.SitOnGround)
            {
                ClientSelf.Stand();
            }
            else
            {
                uint sit = ClientSelf.SittingOn;
                if (sit != 0)
                {
                    UnPhantom = WorldSystem.GetSimObject(WorldSystem.GetPrimitive(sit));
                    UnPhantom.MakeEnterable(this);
                    ClientSelf.Stand();
                }
            }
            return UnPhantom;
        }

        public void StopMoving()
        {
            lock (TrackerLoopLock)
            {
                ApproachPosition = null;
            }
                AgentManager ClientSelf = Client.Self;
                ClientSelf.AutoPilotCancel();
                AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
                //  ClientMovement. AlwaysRun = false;
                ClientMovement.AtNeg = false;
                ClientMovement.AtPos = false;
                //ClientMovement.AutoResetControls = true;
                //   ClientMovement. Away = false;
                ClientMovement.FastAt = false;
                ClientMovement.FastLeft = false;
                ClientMovement.FastUp = false;
                // ClientMovement.FinishAnim = true;
                //  ClientMovement. Fly = false;
                ClientMovement.LButtonDown = false;
                ClientMovement.LButtonUp = false;
                ClientMovement.LeftNeg = false;
                ClientMovement.LeftPos = false;
                ClientMovement.MLButtonDown = false;
                ClientMovement.MLButtonUp = false;
                // ClientMovement. Mouselook = false;
                ClientMovement.NudgeAtNeg = false;
                ClientMovement.NudgeAtPos = false;
                ClientMovement.NudgeLeftNeg = false;
                ClientMovement.NudgeLeftPos = false;
                ClientMovement.NudgeUpNeg = false;
                ClientMovement.NudgeUpPos = false;
                ClientMovement.PitchNeg = false;
                ClientMovement.PitchPos = false;
                //ClientMovement. SitOnGround = false;
                //ClientMovement. StandUp = false;
                ClientMovement.Stop = true;
                ClientMovement.TurnLeft = false;
                ClientMovement.TurnRight = false;
                ClientMovement.UpdateInterval = 0;
                ClientMovement.UpNeg = false;
                ClientMovement.UpPos = false;
                ClientMovement.YawNeg = false;
                ClientMovement.YawPos = false;

                ClientMovement.SendUpdate();
            
        }


        object TrackerLoopLock = new object();

        void TrackerLoop()
        {
            AgentManager ClientSelf = Client.Self;
            AgentManager.AgentMovement ClientMovement = ClientSelf.Movement;
            bool StartedFlying = false;// !IsFloating;
            Boolean justStopped = false;
            Random somthing = new Random(Environment.TickCount);// We do stuff randomly here
            while (true)
            {
                Vector3 targetPosition;
                lock (TrackerLoopLock)
                {
                    // Debug("TrackerLoop: " + Thread.CurrentThread);
                    if (ApproachPosition == null)
                    {
                        Thread.Sleep(500);
                        continue;                    
                    }
                    targetPosition = ApproachPosition.GetUsePosition();
                }
                //ApproachDistance = ApproachPosition.GetSizeDistance();
                try
                {
                    
                    float UpDown = targetPosition.Z - ClientSelf.SimPosition.Z;
                    float ZDist = Math.Abs(UpDown);
                    if (UpDown > 1)
                    {
                        targetPosition.Z = GetSimPosition().Z + 0.2f; // incline upward
                    }
                    else
                    {
                        targetPosition.Z = GetSimPosition().Z;
                    }
                    // allow flight
                    if (ZDist > ApproachDistance)
                    {
                        if (!ClientMovement.Fly)
                        {
                            if (!StartedFlying)
                            {
                                //if (UpDown > 0) ClientMovement.NudgeUpPos = true;
                                // ClientMovement.SendUpdate(false);
                                // ClientSelf.Fly(true);                                 
                                StartedFlying = true;
                            }
                        }
                    }
                    else
                    {
                        if (StartedFlying)
                        {
                            ClientMovement.NudgeUpPos = false;
                            // ClientSelf.Fly(false);
                            StartedFlying = false;
                        }
                    }

                    if (!StartedFlying)
                    {
                        ClientMovement.NudgeUpPos = false;
                        // targetPosition.Z = ApproachPosition.Z;
                    }
                  //  Vector3 Destination = ApproachPosition;
                    float curDist = Vector3.Distance(GetSimPosition(), targetPosition);
                    Client.Self.Movement.TurnToward(targetPosition);
                    if (curDist > ApproachDistance)
                    {
                        //ClientMovement.SendUpdate();
                        if (curDist < (ApproachDistance * 1.25))
                        {
                            //MoveFast(ApproachPosition);
                            //MoveSlow(ApproachPosition);
                            //Thread.Sleep(100);
                            Client.Self.Movement.AtPos = true;
                            Client.Self.Movement.SendUpdate(true);
                            Thread.Sleep(125);
                            Client.Self.Movement.Stop = true;
                            Client.Self.Movement.AtPos = false;
                            Client.Self.Movement.NudgeAtPos = true;
                            Client.Self.Movement.SendUpdate(true);
                            Thread.Sleep(100);
                            Client.Self.Movement.NudgeAtPos = false;
                            Client.Self.Movement.SendUpdate(true);
                            Thread.Sleep(100);
                        }
                        else
                        {
                            Client.Self.Movement.AtPos = true;
                            Client.Self.Movement.UpdateInterval = 0; //100
                            Client.Self.Movement.SendUpdate(true);
                            //(int)(25 * (1 + (curDist / followDist)))
                            Thread.Sleep(somthing.Next(25, 100));
                         //   MoveFast(ApproachPosition);
                        //    if (ApproachPosition!=null) MoveSlow(ApproachPosition);
                        }
                        justStopped = true;
                    }
                    else
                    {
                        if (justStopped)
                        {
                            Client.Self.Movement.TurnToward(targetPosition);
                            ClientMovement.AtPos = false;
                            ClientMovement.UpdateInterval = 0;
                            //ClientMovement.StandUp = true;
                            //ClientMovement.SendUpdate();
                            ClientMovement.FinishAnim = true;
                            ClientMovement.Stop = true;
                            ClientMovement.SendUpdate(false);
                            Thread.Sleep(25);
                            // WorldSystem.TheSimAvatar.StopMoving();
                            justStopped = false;
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }


                    }

                }
                catch (Exception e)
                {
                    Debug("" + e);
                }

            }
        }

        public override SimWaypoint GetWaypoint()
        {
            SimPathStore PathStore = WorldSystem.SimPaths;
            Vector3 v3 = GetSimPosition();
            SimWaypoint swp = PathStore.CreateClosestWaypoint(v3);
            float dist = Vector3.Distance(v3, swp.GetSimPosition());
            if (!swp.Passable)
            {
                WorldSystem.output("CreateClosestWaypoint: " + v3 + " <- " + dist + " -> " + swp + " " + this);
            }
            return swp;
            //            return PathStore.CreateClosestWaypointBox(v3, 4f);
        }


        public void TurnToward(SimPosition targetPosition)
        {
            Client.Self.Movement.TurnToward(targetPosition.GetSimPosition());
        }

        public void SetMoveTarget(SimPosition target)
        {
            lock (TrackerLoopLock)
            {
                if (target != ApproachPosition)
                {
                    StopMoving();
                }
                ApproachPosition = target;
            }
        }

        float ApproachDistance = 2f;
        public SimPosition ApproachPosition;

        readonly Thread ApproachThread;//= new Thread(TrackerLoop);
      /// <summary>
      /// 
      /// </summary>
      /// <param name="end"></param>
      /// <param name="maxDistance"></param>
      /// <param name="maxSeconds"></param>
      /// <returns></returns>
        public bool MoveTo(Vector3 end, float maxDistance, int maxSeconds)
        {
            if (false)
            {
				         
				StopMoving();
                bool MadeIt = AutoGoto(end,maxDistance,maxSeconds*1000);
                StopMoving();
                return MadeIt;
            }
            lock (TrackerLoopLock)
            {
                SimPathStore PathStore = WorldSystem.SimPaths;
                SimWaypoint P = PathStore.CreateFirstNode(end.X,end.Y);
                ApproachDistance = maxDistance;
                ApproachPosition = P;
            }
            for (int i = 0; i < maxSeconds; i++)
            {
                Thread.Sleep(1000);
                //Application.DoEvents();
                float currentDist = Vector3.Distance(end,GetSimPosition());

                if (currentDist > maxDistance)
                {
                    continue;
                }
                else
                {
                   // StopMoving();
                    return true;
                }
            }
            StopMoving();
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="maxDistance"></param>
        /// <returns></returns>
        public float Approach(SimObject obj, float maxDistance)
        {
            BotClient Client = GetGridClient();
            // stand up first
            SimObject UnPhantom = StandUp();
            // make sure it not going somewhere
            // set the new target
            ApproachDistance = obj.GetSizeDistance() + 0.5f;
            string str = "Approaching " + obj + " " + DistanceVectorString(obj) + " to get " + ApproachDistance;
            Debug(str);
            obj.MakeEnterable(this);
            if (!MoveTo(obj.GetSimPosition(), obj.GetSizeDistance() + 0.5f, 12))
            {
                GotoTarget(obj);
            }
            if (UnPhantom != null)
                UnPhantom.RestoreEnterable(this);

            return Distance(obj);
        }

        int TurnAvoid = 90;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="IsFake"></param>
        /// <returns></returns>
        public bool TryGotoTarget(SimPosition pos, out bool IsFake)
        {
            IsFake = false;
            SimMoverState state = SimMoverState.TRYAGAIN;
            while (state == SimMoverState.TRYAGAIN)
            {
                SimWaypoint target = pos.GetWaypoint();
                IList<SimRoute> routes = GetRouteList(target, out IsFake);
                if (routes == null) return false;
                SimRouteMover ApproachPlan = new SimRouteMover(this, routes, pos.GetSimPosition(), pos.GetSizeDistance());
                state = ApproachPlan.Goto();
                if (state == SimMoverState.COMPLETE) return true;
            }
            return false;
            
            //== SimMoverState.COMPLETE;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool GotoTarget(SimPosition pos) {



            if (SimPathStore.OtherPathFinder)
            {
                return GotoSimVector(pos.GetUsePosition(), pos.GetSizeDistance());
            }

            if (AutoGoto(pos.GetSimPosition(), pos.GetSizeDistance(), 2000))
            {
                Debug("EASY GotoTarget: " + pos);
                return true;
            }
            bool IsFake;
            for (int i = 0; i < 19; i++)
            {
                Debug("PLAN GotoTarget: " + pos);
                // StopMoving();
                if (TryGotoTarget(pos,out IsFake))
                {
                    StopMoving();
                    TurnToward(pos);
                    Debug("SUCCESS GotoTarget: " + pos);
                    return true;
                }

                //TurnToward(pos);
                float posDist = Vector3.Distance(GetSimPosition(), pos.GetSimPosition());
                if (posDist <= pos.GetSizeDistance() + 0.5)
                {
                    Debug("OK GotoTarget: " + pos);
                    return true;
                }
                TurnAvoid += 115;
                while (TurnAvoid > 360)
                {
                    TurnAvoid -= 360;
                }
                //Vector3 newPost = GetLeftPos(TurnAvoid, 4f);

                //StopMoving();
                //Debug("MOVELEFT GotoTarget: " + pos);
                //MoveTo(newPost, 2f, 4);
                if (IsFake) break;
            }
            Debug("FAILED GotoTarget: " + pos);
            return false;
        }

        private bool GotoSimVector(Vector3 vector3, float finalDistance)
        {

            SimPathStore PathStore = WorldSystem.SimPaths;
            int OneCount = 0;
            Client.Self.Movement.TurnToward(vector3);
            if (Vector3.Distance(GetSimPosition(), vector3) < finalDistance) return true;
            for (int trial = 0; trial < 25; trial++)
            {
                StopMoving();
                Application.DoEvents();             
                Vector3 start = GetUsePosition();

                if (!PathStore.IsPassable(start))
                {
                   start = MoveToPassableArround(start);
                }
                Vector3 end = vector3;


                List<Vector3> v3s = (List<Vector3>)PathStore.GetV3Route(start, end);
                if (v3s.Count > 1)
                {
                    if (Vector3.Distance(v3s[0], start) > Vector3.Distance(v3s[v3s.Count - 1], start))
                        v3s.Reverse();
                }
                else
                {
                    MoveToPassableArround(GetSimPosition());
                  //  GetUsePosition();
                    if (OneCount > 3) return false;
                    OneCount++;
                }

                Debug("Path {1}: {0} " ,v3s.Count ,trial);
                if (FollowPath(v3s, vector3, finalDistance)) return true;
                if (Vector3.Distance(GetSimPosition(), vector3) < finalDistance) return true;

            }
            return false;
        }


        float LastTurn = 0f;
        public Vector3 MoveToPassableArround(Vector3 start)
        {
            SimPathStore PathStore = WorldSystem.SimPaths;
            float A45 = 45f / SimPathStore.RAD2DEG;
            for (float angle = A45; angle < SimPathStore.PI2; angle += A45)
            {
                Vector3 next = ZAngleVector(angle + LastTurn) * 2 + start;
                 if (PathStore.IsPassable(next))
                 {
                     if (MoveTo(next,1f,3)) {
                         LastTurn += angle;  // update for next use
                         if (LastTurn > SimPathStore.PI2)
                             LastTurn -= SimPathStore.PI2;
                         return next;
                     }
                 }
            }
            return start;
        }

        private bool FollowPath(List<Vector3> v3sIn, Vector3 finalTarget, float finalDistance)
        {
            SimPathStore PathStore = WorldSystem.SimPaths;
            IList<Vector3> v3s = PathStore.GetSimplifedRoute(GetSimPosition(), v3sIn, 10, 8f);
            Debug("FollowPath: {0} -> {1}", v3sIn.Count, v3s.Count);
            float dist = 0.75f;
            int CanSkip = 2;
            int Skipped = 0;
            foreach (Vector3 v3 in v3s)
            {
                //  if (Vector3.Distance(v3, GetSimPosition()) < dist) continue;
                if (!MoveTo(v3, dist, 5))
                {
                    if (Vector3.Distance(GetSimPosition(), finalTarget) < finalDistance) return true;
                    if (!MoveTo(v3, dist, 2))
                    {
                        if (Skipped++ <= CanSkip)
                        {
                            MoveToPassableArround(GetSimPosition());
                            Skipped++;
                            continue;
                        }
                        BlockTowardsVector(v3);
                        return false;
                    }
                }
                else
                {
                    Skipped = 0;
                }

            }
            return true;
        }

        /// <summary>
        /// Blocks points -45 to +45 degrees in front of Bot (assumes the bot is heading toward V3)
        /// </summary>
        /// <param name="v3"></param>
        private void BlockTowardsVector(Vector3 v3)
        {
            OpenNearbyClosedPassages();
            SimPathStore PathStore = WorldSystem.SimPaths;
            Point P1 = PathStore.ToPoint(GetSimPosition());
            Vector3 cp = GetSimPosition();
            Vector3 offset = v3 - cp;
            float ZAngle = (float)Math.Atan2(offset.Y, offset.X);
            Point Last = PathStore.ToPoint(v3);
            float Dist = 0.3f;
            Vector3 b1 = v3;
            while (offset.Length() > 0.1)
            {
                offset *= 0.75f;
                Vector3 blocked = cp + offset;
                Point P2 = PathStore.ToPoint(blocked);
                if (P2 != P1)
                {
                    Dist = offset.Length();
                    Last = P2;
                    b1 = blocked;
                }
            }
            float x = Last.X / PathStore.POINTS_PER_METER;
            float y = Last.Y / PathStore.POINTS_PER_METER;
            BlockPoint(new Vector3( x, y,v3.Z));
            float A45 = 45f / SimPathStore.RAD2DEG;
            Debug("Blocked {0},{1}", x, y);
            Vector3 middle = ZAngleVector(ZAngle) * Dist;
            middle += cp;
            float mdist = Vector3.Distance(middle, b1);
            if (mdist > 0.1)
            {
                Debug("Wierd mdist=" + mdist);
            }
            Dist = 0.4f;
            BlockPoint(ZAngleVector(ZAngle) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle - A45 * 0.5) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle + A45 * 0.5) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle - A45) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle + A45) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle - A45 * 1.5) * Dist + cp);
            BlockPoint(ZAngleVector(ZAngle + A45 * 1.5) * Dist + cp);
            //Dont Run back
            //MoveTo(cp + ZAngleVector(ZAngle - Math.PI) * 2, 1f, 2);
        }

        private void OpenNearbyClosedPassages()
        {
            SimObjectType DOOR = SimTypeSystem.DOOR;
            // look for closed doors
            foreach (SimObject O in GetNearByObjects(2, false))
            {
                if (O.IsTypeOf(DOOR)!=null)
                {
                    O.MakeEnterable(this);
                }
            }
            
            
        }

        private Vector3 ZAngleVector(double ZAngle)
        {
            while (ZAngle <0)
            {
                ZAngle += SimPathStore.PI2;
            }
            while (ZAngle > SimPathStore.PI2)
            {
                ZAngle -= SimPathStore.PI2;
            }
            return new Vector3((float)Math.Sin(ZAngle), (float)Math.Cos(ZAngle), 0);
        }
        /// <summary>
        /// Blocks a point temporarilly (one minute)
        /// </summary>
        /// <param name="vector3"></param>
        private void BlockPoint(Vector3 vector3)
        {
            SimPathStore PathStore = WorldSystem.SimPaths;
            Point P = PathStore.ToPoint(vector3);
            Debug("BlockPoint {0},{1}", P.X / PathStore.POINTS_PER_METER, P.Y / PathStore.POINTS_PER_METER);
            byte oldValue = PathStore.GetNodeQuality(vector3);
            if (oldValue == 0) // aready blocked
                return;
            PathStore.SetNodeQuality(vector3, 0);
            new Thread(new ThreadStart(delegate()
            {
                Thread.Sleep(60000);
                byte newValue = PathStore.GetNodeQuality(vector3);
                if (newValue != 0)
                {
                    // its been changed by something else since we set to Zero
                    Debug("BlockPoint Thread out of date {0} value changed to {1}", vector3, newValue);
                }
                else
                {
                    PathStore.SetNodeQuality(vector3, oldValue);
                    Debug("Unblock {0} value reset to {1}", vector3, oldValue);
                }
            })).Start();
        }

        private void BlockForwardPos()
        {
            SimPathStore PathStore = WorldSystem.SimPaths;
            Point P1 = PathStore.ToPoint(GetSimPosition());
            Point Last = Point.Empty;
            for (float dist = 0.1f; dist < 0.75f; dist += 0.14f)
            {
                Vector3 blocked = GetLeftPos(0, dist);
                Point P2 = PathStore.ToPoint(blocked);
                if (P2 != P1 && Last != P2)
                {
                    BlockPoint(blocked);
                    Debug("Blocked {0},{1}", P2.X / PathStore.POINTS_PER_METER, P2.Y / PathStore.POINTS_PER_METER);
                    Last = P2;
                }            
            }
            for (float dist = 0.1f; dist < 0.75f; dist += 0.14f)
            {
                Vector3 blocked = GetLeftPos(45, dist);
                Point P2 = PathStore.ToPoint(blocked);
                if (P2 != P1 && Last != P2)
                {
                    BlockPoint(blocked);
                    Debug("Blocked {0},{1}", P2.X / PathStore.POINTS_PER_METER, P2.Y / PathStore.POINTS_PER_METER);
                    Last = P2;
                }
            }
            for (float dist = 0.1f; dist < 0.75f; dist += 0.14f)
            {
                Vector3 blocked = GetLeftPos(360-45, dist);
                Point P2 = PathStore.ToPoint(blocked);
                if (P2 != P1 && Last != P2)
                {
                    BlockPoint(blocked);
                    Debug("Blocked {0},{1}", P2.X / PathStore.POINTS_PER_METER, P2.Y / PathStore.POINTS_PER_METER);
                    Last = P2;
                }
            }

            Last = Point.Empty;
            for (float dist = 0.1f; dist < 0.75f; dist += 0.14f)
            {
                Vector3 blocked = GetLeftPos(0, dist);
                Point P2 = PathStore.ToPoint(blocked);
                if (P2 != P1 && Last != P2)
                {
                    PathStore.SetPassable(blocked.X, blocked.Y);
                    Debug("Unblocked {0},{1}", P2.X / PathStore.POINTS_PER_METER, P2.Y / PathStore.POINTS_PER_METER);
                    Last = P2;
                }
            }
            //Last = Point.Empty;
            //for (float dist = 0.0f; dist < 1f; dist += 0.14f)
            //{
            //    Vector3 blocked = GetLeftPos(180, dist);
            //    Point P2 = PathStore.ToPoint(blocked);
            //    if (P2 != Last)
            //    {
            //        PathStore.SetPassable(blocked.X, blocked.Y);
            //        Debug("Unblocked {0},{1}", P2.X / PathStore.POINTS_PER_METER, P2.Y / PathStore.POINTS_PER_METER);
            //        Last = P2;
            //    }
            //}
        }


             public bool AutoGoto(Vector3 target3, float dist, long maxMs)
        {
            long endAt = Environment.TickCount + maxMs;
            Vector2 target = new Vector2(target3.X, target3.Y);
            float d = Vector3.Distance(GetSimPosition(), target3);
            if (d < dist) return true;
            float ld = d;
            float traveled = 0.0f;
            uint x, y;
            // Vector2 P = Position();
            Utils.LongToUInts(Client.Network.CurrentSim.Handle, out x, out y);
            Client.Self.AutoPilot((ulong)(x + target.X), (ulong)(y + target.Y), target3.Z);
            bool AutoPilot = true;
            while (AutoPilot)
            {
                // float moved = Vector2.Distance(P, Position());
                // WriteLine("Moved=" + moved);
                if (d < dist)
                {
                    AutoPilot = false;
                }
                else
                    if (Environment.TickCount > endAt)
                    {
                        AutoPilot = false;
                    }
                    else
                    {
                        Application.DoEvents();
                        d = Vector3.Distance(GetSimPosition(), target3);
                        traveled = ld - d;
                        if (traveled < 0)
                        {
                           // AutoPilot = false;
                        }
                        Client.Self.Movement.TurnToward(target3);
                        ld = d;
                    }
                //    P = Position();
            }
            Client.Self.AutoPilotCancel();
            Client.WorldSystem.TheSimAvatar.StopMoving();
            Client.Self.Movement.TurnToward(target3);
			StopMoving();				
            return Vector3.Distance(GetSimPosition(), target3)<=dist;
        }


        public void Touch(SimObject simObject)
        {
            if (IsLocal())
            {
                Client.Self.Touch(simObject.thePrim.LocalID);
            }
        }
    }


}
