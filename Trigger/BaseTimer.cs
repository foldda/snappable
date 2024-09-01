using Charian;
using Foldda.Automation.Framework;
using Foldda.Automation.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Foldda.Automation.Trigger
{
    public abstract class BaseTimer : BasicDataHandler
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

        public BaseTimer(ILoggingProvider logger) : base(logger) { }

        public override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return null;    // throw new NotImplementedException();
        }

        public override void SetParameter(IConfigProvider config)
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

        public override async Task ProcessData(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                try
                {
                    while (cancellationToken.IsCancellationRequested == false)
                    {
                        int count = TimeTable.Count;
                        if(count == 0)
                        {
                            await Task.Delay(200);
                            ResetTimeTable();   //sub-class adds new entries into the table
                        }
                        else
                        {
                            //used for removing duplicated entries
                            HashSet<DateTime> hashSet = new HashSet<DateTime>();

                            //lopp thru all events and see if any of them shall be fired
                            while (count > 0)
                            {
                                if (TimeTable.TryDequeue(out DateTime t))
                                {
                                    if (t > DateTime.Now)
                                    {
                                        /* use hashset to de-duplicate */
                                        if (hashSet.Add(t)) 
                                        { 
                                            TimeTable.Enqueue(t); //do nothing with future events
                                        }
                                        //else are duplicates
                                    }                                    
                                    else if (t > DateTime.Now.AddSeconds(-1))
                                    {
                                        //fire an event if it's 'just' expired
                                        OutputStorage.Receive(new HandlerEvent(TimerId, t));
                                        Log($"Timer '{TimerId}' fired at {t.ToString("HH:mm:ss")}.");
                                    }
                                    //else the 'long expired' events are dropped

                                    count--;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        await Task.Delay(100);
                    }
                }
                catch (Exception e)
                {
                    Log($"\nHandler operation is stopped due to exception - {e.Message}.");
                }
                finally
                {
                    //Node.LogEvent(Constant.NodeEventType.LastStop);
                    //don't set STATE here, let command and state-table to drive state 
                    Log($"Node handler '{this.GetType().Name}' tasks stopped.");
                }

            });
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