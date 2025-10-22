using Charian;
using Foldda.Automation.Framework;
using Foldda.Automation.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Foldda.Automation.Trigger
{
    public abstract class BaseTimer : BasicSnappable
    {
        // 24-hour clock time of a day, eg "HH:mm:ss", or "mm:ss" if it's a HourlyTimer
        // can have multiple 
        const string TIMEUPS = "trigger-time"; 

        // id for identifying this timer as an event-triggering source
        //const string TIMER_ID = "timer-id"; 
        public ConcurrentQueue<DateTime> TimeTable { get; set; } = new ConcurrentQueue<DateTime>();
        
        //stores time of a day from the settings, used for construct trigger events in the timetable
        protected List<DayTimeSetting> TimeSettings { get; set; } = new List<DayTimeSetting>();

        //
        protected string TimerId { get; set; }

        public BaseTimer(ISnappableManager manager) : base(manager) { }

        public override void Setup(IConfigProvider config)
        {
            TimeSettings.Clear();
            TimeTable = new ConcurrentQueue<DateTime>();

            foreach (string setting in config.GetSettingValues(TIMEUPS))
            {
                string[] tokens = setting.Split(new char[] { ':', '-', ' ' });
                if(tokens.Length==3)
                {
                    TimeSettings.Add(new DayTimeSetting(tokens[0], tokens[1], tokens[2]));
                }
                else if(tokens.Length == 2)
                {
                    TimeSettings.Add(new DayTimeSetting(tokens[0], tokens[1]));
                }
                else
                {
                    throw new Exception($"Invalid time-setting '{setting}', must be 'HH:mm:ss' or 'mm:ss' format");
                }
            }

            //in case the settings are not in order
            TimeSettings.OrderBy(x => x.DailyTime);

            TimerId = config.ConfigFileFullPath;
        }


        /// <summary>
        /// Store a set of DateTime objects into the TimeTable, for the next round of timer events, using DayTimeSettings parameters.
        /// 
        /// The interval of the "round" is sub-class specific/dependent
        /// </summary>
        internal abstract void ResetTimeTable();

        public override Task<int> ProcessInboundMessage(MessageRda message, CancellationToken cancellationToken)
        {
            //this is driven by the handler-manager's heartbeat events
            if (message is MessageRda.HandlerEvent eventMsg && eventMsg.EventSourceId == Manager.UID && eventMsg.EventDetailsRda == Rda.NULL)
            {
                if (TimeTable.Count == 0)
                {
                    ResetTimeTable();   //sub-class adds new entries into the table
                }
                else
                {
                    HashSet<DateTime> futureEventsHashSet = new HashSet<DateTime>();//used for removing duplicated entries

                    //lopp thru all scheduled events and see if any of them are due to be fired
                    while (TimeTable.TryDequeue(out DateTime t))
                    {
                        if (t > DateTime.Now)
                        {
                            /* store, but not triggering, future events */
                            futureEventsHashSet.Add(t);
                        }
                        else if (t > DateTime.Now.AddSeconds(-1))
                        {
                            //fire an event if it's 'just' expired
                            Manager.PostHandlerOutboundMessage(new MessageRda.HandlerEvent(UID, t, new Rda() { ScalarValue = TimerId }));
                            Log($"Timer '{TimerId}' fired at {t.ToString("HH:mm:ss")}.");
                        }
                        //else the 'long expired' events are dropped
                    }

                    //put future events back to the timetable
                    foreach(var t in futureEventsHashSet)
                    {
                        TimeTable.Enqueue(t); 
                    }
                }
            }

            return base.ProcessInboundMessage(message, cancellationToken);
        }


        protected class DayTimeSetting
        {
            enum INDEX : int { HOUR = 0, MINUTE = 1, SECOND = 2 }
            internal DayTimeSetting(string hour, string minute, string second)
            {
                Hour = checkRange(int.Parse(hour), 24);
                Minute = checkRange(int.Parse(minute), 60);
                Second = checkRange(int.Parse(second), 60);
            }

            int checkRange(int v, int max)
            {
                if (v >= 0 && v < max)
                {
                    return v;
                }
                else
                {
                    throw new Exception($"Time element setting value {v} is outside valid range (0-{max - 1}).");
                }
            }

            internal DayTimeSetting(string minute, string second)
            {
                Minute = int.Parse(minute);
                Second = int.Parse(second);
            }

            internal DateTime HourlyTime
            {
                get
                {
                    var nowTime = DateTime.Now;
                    return new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, this.Minute, this.Second);
                }
            }

            internal DateTime DailyTime
            {
                get
                {
                    var nowTime = DateTime.Now;
                    return new DateTime(nowTime.Year, nowTime.Month, nowTime.Day,
                                (this.Hour >= 0 ? this.Hour : nowTime.Hour), this.Minute, this.Second);
                }
            }

            internal int Hour { get; set; } = -1;
            internal int Minute { get; set; }
            internal int Second { get; set; }
        }
    }
}