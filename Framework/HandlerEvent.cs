using Charian;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Foldda.Automation.Framework
{
    //
    /// <summary>
    /// HandlerEvent is a generic type of data record that is passed between handlers. It has a time component
    /// and a handler-dependent "details".
    /// 
    /// A typical pattern is, a hanlder, along with its data-processing logic, can also (optionally) define 
    /// input-context Rda, and/or an output-context Rda, and these Rda are embeded inside HandlerEvent to 
    /// trigger/respond colaborative events between handlers. I.e., connected hanlder 'understand' each other's
    /// trigger event context, and be able to respond accordingly.
    /// 
    /// </summary>
    public class HandlerEvent : IRda
    {
        enum HANDLER_EVENT : int { EVENT_SOURCE_ID, EVENT_TIME_TOKENS, EVENT_CONTEXT }
        enum TIME_TOKEN : int { YEAR = 0, MONTH = 1, DAY = 2, HOUR = 3, MINUTE = 4, SECOND = 5 }

        public string EventSourceId { get; set; }
        public DateTime EventTime { get; set; }
        public Rda EventDetailsRda { get; set; } = new Rda();  //event "payload"

        public HandlerEvent(string sourceId, DateTime time)
        {
            EventSourceId = sourceId;
            EventTime = time;
        }

        public HandlerEvent(string sourceId, List<string> dateTimeTokens)
        {
            EventSourceId = sourceId;
            EventTime = MakeDateTime(dateTimeTokens);
        }

        private DateTime MakeDateTime(List<string> dateTimeTokens)
        {
            if(dateTimeTokens.Count < 3)
            {
                throw new Exception($"Date-time string has only {dateTimeTokens.Count} tokens, expecting minumum 3 (year, month, and day), plus optionally hour, minute, and second.");
            }

            int[] tokens = new int[6];
            for(int i = 0; i < tokens.Length; i++)
            {
                if(i < dateTimeTokens.Count)
                {
                    tokens[i] = int.Parse(dateTimeTokens[i]);
                }
                else
                {
                    break;
                }
            }

            return new DateTime(tokens[(int)TIME_TOKEN.YEAR], tokens[(int)TIME_TOKEN.MONTH], tokens[(int)TIME_TOKEN.DAY],
                                tokens[(int)TIME_TOKEN.HOUR], tokens[(int)TIME_TOKEN.MINUTE], tokens[(int)TIME_TOKEN.SECOND]);
        }

        public HandlerEvent(Rda rda)
        {
            FromRda(rda);
        }

        public override string ToString()
        { 
            return $"{EventSourceId} - {EventTime}";
        }

        public Rda ToRda()
        {
            string[] tokens = new string[6];
            tokens[(int)TIME_TOKEN.YEAR] = EventTime.Year.ToString();
            tokens[(int)TIME_TOKEN.MONTH] = EventTime.Month.ToString();
            tokens[(int)TIME_TOKEN.DAY] = EventTime.Day.ToString();
            tokens[(int)TIME_TOKEN.HOUR] = EventTime.Hour.ToString();
            tokens[(int)TIME_TOKEN.MINUTE] = EventTime.Minute.ToString();
            tokens[(int)TIME_TOKEN.SECOND] = EventTime.Second.ToString();

            Rda result = new Rda();
            result[(int)HANDLER_EVENT.EVENT_SOURCE_ID].ScalarValue = EventSourceId;
            result[(int)HANDLER_EVENT.EVENT_TIME_TOKENS].ChildrenValueArray = tokens;
            result[(int)HANDLER_EVENT.EVENT_CONTEXT] = EventDetailsRda.ToRda();

            return result;
        }

        public IRda FromRda(Rda rda)
        {
            EventSourceId = rda[(int)HANDLER_EVENT.EVENT_SOURCE_ID].ScalarValue;

            List<string> timeValueTokensList = 
                rda[(int)HANDLER_EVENT.EVENT_TIME_TOKENS].ChildrenValueArray.ToList();
            EventTime = MakeDateTime(timeValueTokensList);

            EventDetailsRda = rda[(int)HANDLER_EVENT.EVENT_CONTEXT];

            return this;
        }
    }
}