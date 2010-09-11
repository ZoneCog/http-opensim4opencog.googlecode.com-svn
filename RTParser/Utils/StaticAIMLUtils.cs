using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using RTParser.Database;
using RTParser.Variables;

namespace RTParser.Utils
{
    public class StaticAIMLUtils : TextPatternUtils
    {
        public static readonly Func<Unifiable> EmptyFunct = (() => Unifiable.Empty);

        public static readonly Func<string> NullStringFunct = (() => null);

        public static readonly ICollection<string> PushableAttributes = new HashSet<string>
                                                                            {
                                                                            };

        /// <summary>
        /// Attributes that we use from AIML not intended to be stacked into user dictionary
        /// </summary>
        public static readonly ICollection<string> ReservedAttributes =
            new HashSet<string>
                {
                    "name",
                    "var",
                    "index",
                    "default",
                    "defaultValue",
                    "match",
                    "matches",
                    "existing",
                    "ifUnknown",
                    "user",
                    "bot",
                    "value",
                    "type",
                    "value",
                    "id",
                    "graph",
                    "size",
                    "evidence",
                    "prop",
                    "min",
                    "max",
                    "threshold",
                    "to",
                    "from",
                    "max",
                    "wordnet",
                    "whword",
                    "pos",
                    "constant",
                    "id",
                };

        public static readonly List<string> TagsRecurseToFlatten = new List<string>
                                                                       {
                                                                           "template",
                                                                           "pattern",
                                                                           "that",
                                                                           "sapi",
                                                                           "node",
                                                                           "pre",
                                                                           "bold",
                                                                       };

        public static readonly List<string> TagsWithNoOutput = new List<string>
                                                                   {
                                                                       "#comment",
                                                                       //    "debug",                                                                   
                                                     
                                                                       "br",
                                                                       "p",
                                                                   };


        public static readonly RenderOptions TemplateSideRendering =
            new RenderOptions()
                {
                    flatten =
                        new List<string>(TagsRecurseToFlatten)
                            {
                                "node",
                            },
                    skip = new List<string>(TagsWithNoOutput)
                               {
                                   "#comment",
                                   "silence",
                                   "bookmark",
                                   "src",
                                   //    "debug",
                               }
                };
        public static readonly RenderOptions PatternSideRendering =
    new RenderOptions()
    {
        flatten =
            new List<string>(TagsRecurseToFlatten) { "a", },
        skip =
            new List<string>(TagsWithNoOutput)
                            {
                                "#comment",
                                "silence",
                                "bookmark",
                                "src",
                                "think",
                                //    "debug",
                            }
    };

        public static bool DebugSRAIs = true;
        public static Dictionary<XmlNode, StringBuilder> ErrorList = new Dictionary<XmlNode, StringBuilder>();
        public static bool NoRuntimeErrors = false;
        public static readonly XmlNode PatternStar = StaticXMLUtils.getNode("<pattern>*</pattern>");
        public static readonly XmlNode ThatStar = StaticXMLUtils.getNode("<that>*</that>");
        public static readonly XmlNode TheTemplateOverwrite = StaticXMLUtils.getNode("<template></template>");
        public static readonly XmlNode TopicStar = StaticXMLUtils.getNode("<topic name=\"*\"/>");
        public static readonly XmlNode XmlStar = PatternStar.FirstChild;
        public static bool ThatWideStar;
        public static bool useInexactMatching;
        public static OutputDelegate userTraceRedir;
        public static bool TrackTemplates = false; // to save mememory

        public static R FromLoaderOper<R>(Func<R> action, GraphMaster gm)
        {
            OutputDelegate prev = userTraceRedir;
            try
            {
                userTraceRedir = gm.writeToLog;
                try
                {
                    lock (ErrorList)
                    {
                        lock (gm.LockerObject)
                        {
                            return action();
                        }
                    }
                }
                catch (Exception e)
                {
                    RTPBot.writeDebugLine("ERROR: LoaderOper {0}", e);
                    if (NoRuntimeErrors) return default(R);
                    throw;
                    //return default(R);
                }
            }
            finally
            {
                userTraceRedir = prev;
            }
        }

        public static ThreadStart EnterTag(Request request, XmlNode templateNode, SubQuery query)
        {
            bool needsUnwind = false;
            object thiz = (object) query ?? request;
            ISettingsDictionary dict = query ?? request.TargetSettings;
            XmlAttributeCollection collection = templateNode.Attributes;
            if (collection != null && collection.Count > 0)
            {
                // graphmaster
                GraphMaster oldGraph = request.Graph;
                GraphMaster newGraph = null;
                // topic
                Unifiable oldTopic = request.Topic;
                Unifiable newTopic = null;

                // that
                Unifiable oldThat = request.That;
                Unifiable newThat = null;

                UndoStack savedValues = null;

                foreach (XmlAttribute node in collection)
                {
                    switch (node.Name.ToLower())
                    {
                        case "graph":
                            {
                                string graphName = ReduceStar<string>(node.Value, query, dict);
                                if (graphName != null)
                                {
                                    GraphMaster innerGraph = request.TargetBot.GetGraph(graphName, oldGraph);
                                    needsUnwind = true;
                                    if (innerGraph != null)
                                    {
                                        if (innerGraph != oldGraph)
                                        {
                                            request.Graph = innerGraph;
                                            newGraph = innerGraph;
                                            request.writeToLog("ENTERING: {0} as {1} from {2}",
                                                               graphName, innerGraph, oldGraph);
                                        }
                                        else
                                        {
                                            newGraph = innerGraph;
                                        }
                                    }
                                    else
                                    {
                                        oldGraph = null; //?
                                    }
                                }
                            }
                            break;
                        case "topic":
                            {
                                newTopic = ReduceStar<Unifiable>(node.Value, query, dict);
                                if (newTopic != null)
                                {
                                    if (newTopic.IsEmpty) newTopic = "Nothing";
                                    needsUnwind = true;
                                    request.Topic = newTopic;
                                }
                            }
                            break;
                        case "that":
                            {
                                newThat = ReduceStar<Unifiable>(node.Value, query, dict);
                                if (newThat != null)
                                {
                                    if (newThat.IsEmpty) newThat = "Nothing";
                                    needsUnwind = true;
                                    request.That = newThat;
                                }
                            }
                            break;

                        default:
                            {
                                string n = node.Name;
                                lock (ReservedAttributes)
                                {
                                    if (ReservedAttributes.Contains(n))
                                        continue;
                                    bool prev = NamedValuesFromSettings.UseLuceneForGet;
                                    try
                                    {
                                        NamedValuesFromSettings.UseLuceneForGet = false;
                                        if (!dict.containsSettingCalled(n))
                                        {
                                            ReservedAttributes.Add(n);
                                            request.writeToLog("ReservedAttributes: {0}", n);
                                        }
                                        else
                                        {
                                            if (!PushableAttributes.Contains(n))
                                            {
                                                PushableAttributes.Add(n);
                                                request.writeToLog("PushableAttributes: {0}", n);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        NamedValuesFromSettings.UseLuceneForGet = prev;
                                    }
                                }

                                // now require temp vars to say  with_id="tempId"
                                // to set the id="tempid" teporarily while evalig tags
                                if (!n.StartsWith("with_"))
                                {
                                    continue;
                                }
                                else
                                {
                                    n = n.Substring(5);
                                }

                                Unifiable v = ReduceStar<Unifiable>(node.Value, query, dict);
                                UndoStack.FindUndoAll(thiz);
                                savedValues = savedValues ?? UndoStack.GetStackFor(thiz);
                                //savedValues = savedValues ?? query.GetFreshUndoStack();
                                savedValues.pushValues(dict, n, v);
                                needsUnwind = true;
                            }
                            break;
                    }
                }

                // unwind
                if (needsUnwind)
                {
                    return () =>
                               {
                                   try
                                   {
                                       if (savedValues != null)
                                       {
                                           savedValues.UndoAll();
                                       }
                                       if (newGraph != null)
                                       {
                                           GraphMaster cg = request.Graph;
                                           if (cg == newGraph)
                                           {
                                               request.writeToLog("LEAVING: {0}  back to {1}", request.Graph, oldGraph);
                                               request.Graph = oldGraph;
                                           }
                                           else
                                           {
                                               request.writeToLog(
                                                   "WARNING: UNWIND GRAPH UNEXPECTED CHANGE {0} FROM {1} SETTING TO {2}",
                                                   cg, newGraph, oldGraph);
                                               request.Graph = oldGraph;
                                           }
                                       }
                                       if (newTopic != null)
                                       {
                                           Unifiable ct = request.Topic;
                                           if (newTopic == ct)
                                           {
                                               request.Topic = oldTopic;
                                           }
                                           else
                                           {
                                               request.writeToLog(
                                                   "WARNING: UNWIND TOPIC UNEXPECTED CHANGE {0} FROM {1} SETTING TO {2}",
                                                   ct, newTopic, oldTopic);
                                               request.Topic = oldTopic;
                                           }
                                       }
                                       if (newThat != null)
                                       {
                                           Unifiable ct = request.That;
                                           if (newThat == ct)
                                           {
                                               request.That = oldThat;
                                           }
                                           else
                                           {
                                               request.writeToLog(
                                                   "WARNING: UNWIND THAT UNEXPECTED CHANGE {0} FROM {1} SETTING TO {2}",
                                                   ct, newThat, oldThat);
                                               request.That = oldThat;
                                           }
                                       }
                                   }
                                   catch (Exception ex)
                                   {
                                       request.writeToLog("ERROR " + ex);
                                   }
                               };
                }
            }
            return () => { };
        }


        public static bool ContainsAiml(Unifiable unifiable)
        {
            String s = unifiable.AsString();
            if (s.Contains(">") && s.Contains("<")) return true;
            if (s.Contains("&"))
            {
                return true;
            }
            return false;
        }

        internal static bool AimlSame(XmlNode info, XmlNode Output)
        {
            return info.Name == Output.Name && info.OuterXml == Output.OuterXml;
        }

        public static bool AimlSame(string xml1, string xml2)
        {
            if (xml1 == xml2) return true;
            if (xml1 == null) return String.IsNullOrEmpty(xml2);
            if (xml2 == null) return String.IsNullOrEmpty(xml1);
            xml1 = MakeAimlMatchable(xml1);
            xml2 = MakeAimlMatchable(xml2);
            if (xml1 == xml2) return true;
            return XmlSame(xml1, xml2);
        }

        public static string MakeAimlMatchable(string xml1)
        {
            if (xml1 == null) return xml1;
            string t =
                CleanWhitepaces(MakeMatchable(xml1).Replace(" index=\"1\"", " ").Replace(" index=\"1,1\"", " "));
            // t = t.Replace("<star index=\"1\"/>", " * ");
            // t = t.Replace("<star/>", " * ");
            //t = t.Replace("<sr/>", " * ");
            // t = t.Replace("  ", " ").Trim();
            return t;
        }

        public static int FromInsideLoaderContext(XmlNode currentNode, Request request, SubQuery query, Func<int> doit)
        {
            int total = 0;
            query = query ?? request.CurrentQuery;
            //Result result = query.Result;
            RTPBot RProcessor = request.TargetBot;
            AIMLLoader prev = RProcessor.Loader;
            try
            {
                // RProcessor.Loader = this;
                // Get a list of the nodes that are children of the <aiml> tag
                // these nodes should only be either <topic> or <category>
                // the <topic> nodes will contain more <category> nodes
                string currentNodeName = currentNode.Name.ToLower();

                ThreadStart ts = EnterTag(request, currentNode, query);
                try
                {
                    total += doit();
                }
                finally
                {
                    ts();
                }
            }
            finally
            {
                RProcessor.Loader = prev;
            }
            return total;
        }

        /*
        public static int NonAlphaCount(string input)
        {
            input = CleanWhitepaces(input);
            int na = 0;
            foreach (char s in input)
            {
                if (char.IsLetterOrDigit(s)) continue;
                na++;
            }
            return na;
        }

        public static string NodeInfo(XmlNode templateNode, Func<string, XmlNode, string> funct)
        {
            string s = null;
            XmlNode nxt = templateNode;
            s = funct("same", nxt);
            if (s != null) return s;
            nxt = templateNode.NextSibling;
            s = funct("next", nxt);
            if (s != null) return s;
            nxt = templateNode.PreviousSibling;
            s = funct("prev", nxt);
            if (s != null) return s;
            nxt = templateNode.ParentNode;
            s = funct("prnt", nxt);
            if (s != null) return s;
            return s;
        }
        */

        public static T ReduceStar<T>(IConvertible name, SubQuery query, ISettingsDictionary dict)
            where T : IConvertible
        {
            var nameSplit = name.ToString().Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string nameS in nameSplit)
            {
                Unifiable r = AltStar(nameS, query, dict);
                if (!IsNullOrEmpty(r))
                {
                    PASSTHRU<T>(r);
                }
                continue;
            }
            return PASSTHRU<T>(name);
        }

        public static Unifiable AltStar(string name, SubQuery query, ISettingsDictionary dict)
        {
            try
            {
                if (name.StartsWith("star_"))
                {
                    return GetDictData(query.InputStar, name, 5);
                }
                else if (name.StartsWith("inputstar_"))
                {
                    return GetDictData(query.InputStar, name, 10);
                }
                else if (name.StartsWith("input_"))
                {
                    return GetDictData(query.InputStar, name, 6);
                }
                else if (name.StartsWith("thatstar_"))
                {
                    return GetDictData(query.ThatStar, name, 9);
                }
                else if (name.StartsWith("that_"))
                {
                    return GetDictData(query.ThatStar, name, 5);
                }
                else if (name.StartsWith("topicstar_"))
                {
                    return GetDictData(query.TopicStar, name, 10);
                }
                else if (name.StartsWith("topic_"))
                {
                    return GetDictData(query.TopicStar, name, 6);
                }
                else if (name.StartsWith("guardstar_"))
                {
                    return GetDictData(query.GuardStar, name, 10);
                }
                else if (name.StartsWith("guard_"))
                {
                    return GetDictData(query.GuardStar, name, 6);
                }
                else if (name.StartsWith("@"))
                {
                    Unifiable value = query.Request.TargetBot.SystemExecute(name, null, query.Request);
                    if (!IsNullOrEmpty(value)) return value;
                }
                else if (name.StartsWith("%dictvar_"))
                {
                    Unifiable value = value = GetValue(query, dict, name.Substring(8));
                    if (!IsNullOrEmpty(value)) return value;
                }
                else if (name.StartsWith("%"))
                {
                    Unifiable value = null;
                    string str = name.Substring(1);
                    if (str.StartsWith("bot."))
                    {
                        SettingsDictionary dict2 = query.Request.TargetBot.GlobalSettings;
                        str = str.Substring(4);
                        value = GetValue(query, dict2, str);
                        if (!IsNullOrEmpty(value)) return value;
                    }
                    else if (str.StartsWith("user."))
                    {
                        ISettingsDictionary dict2 = query.Request.Requester;
                        str = str.Substring(5);
                        value = GetValue(query, dict2, str);
                        if (!IsNullOrEmpty(value)) return value;
                    }
                    if (dict != null)
                    {
                        value = GetValue(query, dict, str);
                        if (!IsNullOrEmpty(value)) return value;
                    }
                }
            }
            catch (Exception e)
            {
                RTPBot.writeDebugLine("" + e);
            }
            return null;
        }

        private static Unifiable GetValue(SubQuery query, ISettingsDictionary dict2, string str)
        {
            Unifiable value;
            value = dict2.grabSetting(str);
            return value;
        }

        private static Unifiable GetDictData<T>(IList<T> unifiables, string name, int startChars) where T : IConvertible
        {
            T u = GetDictData0<T>(unifiables, name, startChars);
            string toup = u.ToString(FormatProvider).ToUpper();
            if (string.IsNullOrEmpty(toup)) return PASSTHRU<Unifiable>(u);
            if (char.IsLetterOrDigit(toup[0])) return PASSTHRU<Unifiable>("" + u);
            return PASSTHRU<Unifiable>(u);
        }

        private static T GetDictData0<T>(IList<T> unifiables, string name, int startChars) where T : IConvertible
        {
            string s = name.Substring(startChars);

            if (s == "*" || s == "ALL" || s == "0")
            {
                StringAppendableUnifiableImpl result = Unifiable.CreateAppendable();
                foreach (T u in unifiables)
                {
                    result.Append(u.ToString());
                }
                return PASSTHRU<T>(result);
            }

            int uc = unifiables.Count;

            bool fromend = false;
            if (s.StartsWith("-"))
            {
                fromend = true;
                s = s.Substring(1);
            }

            int i = Int32.Parse(s);

            if (i == 0)
            {
                if (uc == 0) return PASSTHRU<T>("");
            }
            int ii = i - 1;
            if (fromend) ii = uc - i;
            if (uc == 0)
            {
                RTPBot.writeDebugLine(" !ERROR -star underflow! " + i + " in " + name);
                return PASSTHRU<T>(String.Empty);
            }
            if (ii >= uc || ii < 0)
            {
                RTPBot.writeDebugLine(" !ERROR -star badindexed 0 < " + i + " < " + uc + " in " + name);
                return unifiables[ii];
            }
            return unifiables[ii];
        }


        public static bool IsPredMatch(Unifiable required, Unifiable actualValue, SubQuery subquery)
        {
            if (IsNull(required))
            {
                return IsNullOrEmpty(actualValue);
            }
            if (IsNull(actualValue))
            {
                return IsNullOrEmpty(required);
            }
            required = required.Trim();
            if (required.IsAnySingleUnit())
            {
                return !IsNullOrEmpty(actualValue);
            }

            actualValue = actualValue.Trim();

            string requiredToUpper = required.ToUpper();
            if (requiredToUpper == "*")
            {
                return !IsUnknown(actualValue);
            }

            if (IsNullOrEmpty(required) || requiredToUpper == "$MISSING")
            {
                return IsNullOrEmpty(actualValue);
            }

            if (actualValue.WillUnify(required, subquery))
            {
                return true;
            }
            string requiredAsStringReplaceReplace = required.AsString().Replace(" ", "\\s").Replace("*", "[\\sA-Z0-9]+");
            Regex matcher = new Regex("^" + requiredAsStringReplaceReplace + "$",
                                      RegexOptions.IgnoreCase);
            if (matcher.IsMatch(actualValue))
            {
                return true;
            }
            if (requiredToUpper == "UNKNOWN" && (IsUnknown(actualValue)))
            {
                return true;
            }
            return false;
        }


        public static string PadStars(string pattern)
        {
            pattern = pattern.Trim();
            int pl = pattern.Length;
            if (pl == 0) return "~*";
            if (pl == 1) return pattern;
            if (pl == 2) return pattern;
            if (char.IsLetterOrDigit(pattern[pl - 1])) pattern = pattern + " ~*";
            if (char.IsLetterOrDigit(pattern[0])) pattern = "~* " + pattern;
            return pattern;
        }

        public static void PrintResult(Result result, OutputDelegate console, PrintOptions printOptions)
        {
            console("-----------------------------------------------------------------");
            console("Result: " + result.Graph + " Request: " + result.request);
            foreach (Unifiable s in result.InputSentences)
            {
                console("input: \"" + s + "\"");
            }
            PrintTemplates(result.UsedTemplates, console, printOptions);
            foreach (SubQuery s in result.SubQueries)
            {
                console("\n" + s);
            }
            console("-");
            foreach (string s in result.OutputSentences)
            {
                console("outputsentence: " + s);
            }
            console("-----------------------------------------------------------------");
        }

        public static string GetTemplateSource(IEnumerable CI, PrintOptions printOptions)
        {
            if (CI == null) return "";
            StringWriter fs = new StringWriter();
            GraphMaster.PrintToWriter(CI, printOptions, fs, null);
            return fs.ToString();
        }

        public static void PrintTemplates(IEnumerable CI, OutputDelegate console, PrintOptions printOptions)
        {
            GraphMaster.PrintToWriter(CI, printOptions, new OutputDelegateWriter(console), null);
        }

        public static bool IsSilentTag(XmlNode node)
        {
            // if (true) return false;
            if (node.Name == "think") return true;
            if (node.NodeType == XmlNodeType.Text)
            {
                string innerText = node.InnerText;
                if (innerText.Trim().Length == 0)
                {
                    return true;
                }
                return false;
            }
            if (node.Name == "template")
            {
                foreach (XmlNode xmlNode in node.ChildNodes)
                {
                    if (!IsSilentTag(xmlNode)) return false;
                }
                if (node.ChildNodes.Count != 1)
                {
                    return true;
                }
                return true;
            }
            return false;
        }

        internal static string ForOutputTemplate(string sentenceIn)
        {
            return VisibleRendering(getNode("<template>" + sentenceIn + "</template>").ChildNodes, TemplateSideRendering);
        }

        internal static string ForInputTemplate(string sentenceIn)
        {
            string patternSide =
                VisibleRendering(getNode("<template>" + sentenceIn + "</template>").ChildNodes, PatternSideRendering);
            return ForOutputTemplate(patternSide);
        }

        /*
        public string ToEnglish(string sentenceIn, ISettingsDictionary OutputSubstitutions)
        {
            var writeToLog = (OutputDelegate)null;
            if (sentenceIn == null)
            {
                return null;
            }
            sentenceIn = sentenceIn.Trim();
            if (sentenceIn == "")
            {
                return "";
            }
            var sentence = "";
            string xmlsentenceIn = ToEnglishT(sentenceIn);
            if (xmlsentenceIn == "")
            {
                return "";
            }
            sentence = ApplySubstitutions.Substitute(OutputSubstitutions, xmlsentenceIn);
            if (sentenceIn != sentence)
            {
                writeToLog("SUBTS: " + sentenceIn + " -> " + sentence);
            }
            sentence = CleanupCyc(sentence);
            sentence = ApplySubstitutions.Substitute(OutputSubstitutions, sentence);
            return sentence.Trim();
        }


        internal static string ToEnglishT(string sentenceIn)
        {
            string patternSide =
                VisibleRendering(getNode("<template>" + sentenceIn + "</template>").ChildNodes, PatternSideRendering);
            return ForOutputTemplate(patternSide);
        }*/
    }
}