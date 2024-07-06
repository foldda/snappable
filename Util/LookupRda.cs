using Charian;
using System;
using System.Collections.Generic;
using System.Text;

namespace Foldda.Automation.Util
{
    //a time object that can be store in Rda
    public class LookupRda : IRda
    {
        public Dictionary<string, string> Store { get; } = new Dictionary<string, string>();

        public LookupRda() { }

        public LookupRda(Rda rda)
        {
            FromRda(rda);
        }

        //Rda stores a (truncated) 1/1m "ticks" value of a DateTime value
        public IRda FromRda(Rda rda)
        {
            //restores the original ticks value (multiplies the FACTOR), then get the actual time value
            Store.Clear();
            foreach (Rda item in rda.Elements)
            {
                Pair pair = new Pair(item);
                Store.Add(pair.Name, pair.Value);
            }
            return this;
        }

        public Rda ToRda()
        {
            //divid by 1m to shorten the string length (also will reduce the time resolution)
            Rda rda = new Rda();
            foreach (string key in Store.Keys)
            {
                rda.Elements.Add(new Pair(key, Store[key]));
            }
            return rda;
        }

        class Pair : Rda
        {
            internal Pair(Rda rda) : base() 
            {
                FromRda(rda);
            }
            internal Pair(string key, string value) : base()
            {
                Name = key;
                Value = value;
            }

            enum META_DATA : int { NAME, VALUE } //

            public string Name   //
            {
                get => this[(int)META_DATA.NAME].ScalarValue;
                set => this[(int)META_DATA.NAME].ScalarValue = value;
            }

            public string Value   // 
            {
                get => this[(int)META_DATA.VALUE].ScalarValue;
                set => this[(int)META_DATA.VALUE].ScalarValue = value;
            }
        }

    }
}
