using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using Cogbot.Actions.Land;
using Cogbot.Actions.Movement;
using Cogbot.Actions.Scripting;
using Cogbot.Actions.System;
using Cogbot.Actions.WebUtil;
using Cogbot.Library;
using Cogbot.Utilities;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.Utilities;
using Cogbot.Actions;
using System.Threading;
using System.Collections;
using Cogbot.ScriptEngines;
using System.IO;
using Cogbot;
using Cogbot.World;
using System.Drawing;
using Settings=OpenMetaverse.Settings;
using Cogbot.Actions.Agent;
using System.Text;
using Type=System.Type;
#if USE_SAFETHREADS
using Thread = MushDLR223.Utilities.SafeThread;
#endif
//using RadegastTab = Radegast.SleekTab;

// older LibOMV
//using TeleportFlags = OpenMetaverse.AgentManager.TeleportFlags;
//using TeleportStatus = OpenMetaverse.AgentManager.TeleportStatus;

namespace Cogbot
{
    public partial class BotClient : SimEventSubscriber
    {

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<SimObjectEvent> m_EachSimEvent;

        /// <summary>Raises the EachSimEvent event</summary>
        /// <param name="e">An EachSimEventEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnEachSimEvent(SimObjectEvent e)
        {
            if (e.Verb == "On-Log-Message") return;
            if (ExpectConnected == false) return;
            EventHandler<SimObjectEvent> handler = m_EachSimEvent;
            if (handler == null) return;
            List<Delegate> todo = new List<Delegate>();
            lock (m_EachSimEventLock)
            {
                handler = m_EachSimEvent;
                if (handler == null) return;
                AddTodo(handler.GetInvocationList(), todo);
            }
            bool async = todo.Count > 3;

            foreach (var d in todo)
            {
                var del = (EventHandler<SimObjectEvent>)d;
                ThreadStart task = () =>
                {
                    try
                    {
                        del(this, e);
                    }
                    catch (Exception ex)
                    {
                        LogException("OnEachSimEvent Worker", ex);
                    }
                };
                if (async)
                {
                    ThreadPool.QueueUserWorkItem(sync => task());
                }
                else
                {
                    task();
                }
            }
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_EachSimEventLock = new object();

        /// <summary>Triggered when Each Sim Event packet is received,
        /// telling us what our avatar is currently wearing
        /// <see cref="RequestAgentWearables"/> request.</summary>
        public event EventHandler<SimObjectEvent> EachSimEvent
        {
            add { lock (m_EachSimEventLock) { m_EachSimEvent += value; } }
            remove { lock (m_EachSimEventLock) { m_EachSimEvent -= value; } }
        }

        private bool useLispEventProducer = false;
        private LispEventProducer lispEventProducer;
        //List<BotMessageSubscriber> lBotMsgSubscribers = new List<BotMessageSubscriber>();
        public interface BotMessageSubscriber
        {
            void msgClient(string serverMessage);
            void ShuttingDown();
        }
        public void AddBotMessageSubscriber(SimEventSubscriber tcpServer)
        {
            botPipeline.AddSubscriber(tcpServer);
        }
        public void RemoveBotMessageSubscriber(SimEventSubscriber tcpServer)
        {
            botPipeline.RemoveSubscriber(tcpServer);
        }

        public void SendNetworkEvent(string eventName, params object[] args)
        {
            SendPersonalEvent(SimEventType.NETWORK, eventName, args);
        }


        public void SendPersonalEvent(SimEventType type, string eventName, params object[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] is BotClient)
                {
                    args[0] = ((BotClient)args[0]).GetAvatar();
                }
            }
            SimObjectEvent evt = botPipeline.CreateEvent(type, SimEventClass.PERSONAL, eventName, args);
            evt.AddParam("recipientOfInfo", GetAvatar());
            SendPipelineEvent(evt);
        }

        public void SendPipelineEvent(SimObjectEvent evt)
        {
            OnEachSimEvent(evt);
            botPipeline.SendEvent(evt);
        }


        internal string argsListString(IEnumerable list)
        {
            if (scriptEventListener == null) return "" + list;
            return ScriptEventListener.argsListString(list);
        }

        internal string argString(object p)
        {
            if (scriptEventListener == null) return "" + p;
            return ScriptEventListener.argString(p);
        }
        #region SimEventSubscriber Members

        private bool _EventsEnabled = true;
        public bool EventsEnabled
        {
            get
            {
                return _EventsEnabled;// throw new NotImplementedException();
            }
            set
            {
                _EventsEnabled = value;
            }
        }

        #endregion

        void SimEventSubscriber.OnEvent(SimObjectEvent evt)
        {
            if (evt.GetVerb() == "On-Execute-Command")
            {
                ExecuteCommand(evt.GetArgs()[0].ToString(), null, WriteLine);
            }
        }

    }
}