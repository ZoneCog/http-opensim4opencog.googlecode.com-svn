using System;
using System.Xml;
using System.Text;
using RTParser.Utils;

namespace RTParser.AIMLTagHandlers
{
    /// <summary>
    /// The get element tells the AIML interpreter that it should substitute the contents of a 
    /// predicate, if that predicate has a value defined. If the predicate has no value defined, 
    /// the AIML interpreter should substitute the empty Unifiable "". 
    /// 
    /// The AIML interpreter implementation may optionally provide a mechanism that allows the 
    /// AIML author to designate default values for certain predicates (see [9.3.]). 
    /// 
    /// The get element must not perform any text formatting or other "normalization" on the predicate
    /// contents when returning them. 
    /// 
    /// The get element has a required name attribute that identifies the predicate with an AIML 
    /// predicate name. 
    /// 
    /// The get element does not have any content.
    /// </summary>
    public class get : RTParser.Utils.AIMLTagHandler
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be processed</param>
        public get(RTParser.RTPBot bot,
                        RTParser.User user,
                        RTParser.Utils.SubQuery query,
                        RTParser.Request request,
                        RTParser.Result result,
                        XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }

        protected override Unifiable ProcessChange()
        {
            if (this.templateNode.Name.ToLower() == "get")
            {
                if (query.CurrentTemplate != null) query.CurrentTemplate.Rating *= 1.5;
                string name = GetAttribValue("name", templateNodeInnerText.Trim());
                Unifiable defaultVal = GetAttribValue("default", Unifiable.Empty);
                ISettingsDictionary dict = query;
                if (GetAttribValue("type", "") == "bot") dict = request.Proccessor.GlobalSettings;
                Unifiable resultGet = dict.grabSetting(name).Trim();
                if (resultGet.ToValue().ToUpper() == "UNKNOWN") return resultGet + " " + name;
                // if ((!String.IsNullOrEmpty(result)) && (!result.IsWildCard())) return result; // we have a local one
                
                // try to use a global blackboard predicate
                bool newlyCreated;
                RTParser.User gUser = this.user.bot.FindOrCreateUser("UNKNOWN_PARTNER", out newlyCreated);
                Unifiable gResult = gUser.Predicates.grabSetting(name).Trim();

                if ((String.IsNullOrEmpty(resultGet)) && (!String.IsNullOrEmpty(gResult)))
                {
                    // result=nothing, gResult=something => return gResult
                    return gResult;
                }

                if (!String.IsNullOrEmpty(resultGet))
                {
                    if (!String.IsNullOrEmpty(gResult))
                    {
                        // result=*, gResult=something => return gResult
                        if (resultGet.IsWildCard()) return gResult;

                        // result=something, gResult=something => return result
                        return resultGet;
                    }
                    else
                    {
                        // result=something, gResult=nothing => return result
                        return resultGet;
                    }
                }
                // default => return defaultVal
                return defaultVal;
            }
            return Unifiable.Empty;
        }
    }
}
