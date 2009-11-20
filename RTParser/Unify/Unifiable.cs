using System;
using System.Globalization;
using System.Xml;
using RTParser.Utils;

namespace RTParser
{
    abstract public class Unifiable
    {

        public const float UNIFY_TRUE = 0;
        public const float UNIFY_FALSE = 1;

        /// <summary>
        /// This should be overridden!
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This should be overridden!
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static string InnerXmlText(XmlNode templateNode)
        {
            if (templateNode.NodeType == XmlNodeType.Text)
            {
                if (templateNode.InnerXml.Length>0)
                {
                    return templateNode.InnerText + templateNode.InnerXml;                   
                }
                if (templateNode.InnerText.Length > 0)
                {
                    return templateNode.InnerText + templateNode.InnerXml;
                }
                return templateNode.InnerText;
            }
            return templateNode.InnerXml;
        }

                
        public static implicit operator string(Unifiable value)
        {
            if (Object.ReferenceEquals(value, null))
            {
                return null;
            }
            return value.AsString();
        }

        public static implicit operator Unifiable(string value)
        {
            if (value == null) return null;
            Unifiable u = new StringUnifiable(value);
            if (u.IsWildCard())
            {
                
            }
            return u;
        }
        public static Unifiable Empty = new EmptyUnifiable();


        public static Unifiable STAR
        {
            get
            {
                return new StringUnifiable("*");
            }
        }

        static public bool IsTrue(Unifiable v)
        {
            return !IsFalse(v);
        }


        public static Unifiable Join(string p, Unifiable[] fsp, int p_3, int p_4)
        {
            return string.Join(p, FromArrayOf(fsp), p_3, p_4);
        }

        public static Unifiable[] arrayOf(string[] strs)
        {
            Unifiable[] it = new Unifiable[strs.Length];
            for (int i = 0; i < it.Length; i++)
            {
                it[i] = Create(strs[i].Trim());
            }
            return it;
        }

        public static string[] FromArrayOf(Unifiable[] tokens)
        {
            string[] it = new string[tokens.Length];
            for (int i = 0; i < it.Length; i++)
            {
                it[i] = tokens[i].AsString().Trim();
            }
            return it;
        }

        //public static Unifiable Format(string s, params object[] args)
        //{
        //    return string.Format(s, args);
        //}

        //static public bool operator ==(Unifiable t, string s)
        //{
        //    return t.AsString().ToLower() == s.ToLower();
        //}


        static public bool operator ==(Unifiable t, Unifiable s)
        {
            if (IsNull(t))
            {
                return IsNull(s);
            }
            if (IsNull(s))
            {
                return false;
            }

            return t.AsString().ToLower() == s.AsString().ToLower() || t.ToValue().ToLower() == s.ToValue().ToLower();
        }

        public static bool operator !=(Unifiable t, Unifiable s)
        {
            return !(t == s);
        }

        public static bool IsFalse(Unifiable tf)
        {
            if (Object.ReferenceEquals(tf, null)) return true;
            if (Object.ReferenceEquals(tf.Raw, null)) return true;
            return tf.IsFalse();
        }

        public static bool IsNull(Object name)
        {
            if (Object.ReferenceEquals(name, null)) return true;
            return (name is Unifiable && ((Unifiable)name).Raw == null);
        }

        public static Unifiable operator +(Unifiable u, string more)
        {
            return u.AsString() + more;
        }
        public static Unifiable operator +(Unifiable u, Unifiable more)
        {
            return u.AsString() + more.AsString();
        }

        public static Unifiable Create(object p)
        {
            if (p is Unifiable) return (Unifiable) p;
            if (p is string) return new StringUnifiable((string) p);
            // TODO
            if (p is XmlNode) return new StringUnifiable(InnerXmlText((XmlNode) p));
            return new StringUnifiable(p.ToString());
        }

        internal static Unifiable CreateAppendable()
        {
            return new StringUnifiable();
        }

        public static Unifiable ThatTag = Create("TAG-THAT");
        public static Unifiable TopicTag = Create("TAG-TOPIC");

        protected abstract object Raw { get; }
        public virtual bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(ToValue());
            }
        }
        protected virtual bool IsFalse()
        {
            return IsEmpty;            
        }
        public abstract bool IsTag(string s);
        public abstract bool IsMatch(Unifiable unifiable);
        public virtual bool IsWildCard()
        {
            return true;
        }
        public abstract bool IsLazyStar();
        public abstract bool IsLongWildCard();
        public abstract bool IsShortWildCard();
        public abstract float Unify(Unifiable unifiable, SubQuery query);

        public virtual Unifiable ToCaseInsenitive()
        {
            return this;
        }
        public virtual Unifiable Frozen()
        {
            return ToValue();
        }
        public abstract string ToValue();
        public abstract string AsString();
        public virtual Unifiable ToPropper()
        {
            return this;
        }
        public virtual Unifiable Trim()
        {
            return this;
        }

        //public abstract Unifiable[] Split(Unifiable[] unifiables, StringSplitOptions options);
        //public abstract Unifiable[] Split();


        // join functions
        public abstract void Append(Unifiable part);
        public abstract void Clear();

        public abstract Unifiable First();

        public abstract Unifiable Rest();

    }
}

