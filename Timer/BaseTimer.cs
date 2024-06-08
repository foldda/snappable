using Charian;
using Foldda.DataAutomation.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Foldda.DataAutomation.Timer
{
    public abstract class BaseTimer : AbstractDataHandler
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
        string TimerId => HomePath.FullName;

        public BaseTimer(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return null;    // throw new NotImplementedException();
        }

        public override void SetParameters(IConfigProvider config)
        {
            TimeSettings.Clear();

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

           // TimerId = config.GetSettingValue(TIMER_ID, config.ConfigProviderId);
        }


        /// <summary>
        /// Store a set of DateTime objects into the TimeTable, for the next round of timer events, using DayTimeSettings parameters.
        /// 
        /// The interval of the "round" is sub-class specific/dependent
        /// </summary>
        internal abstract void ResetTimeTable();

        public override Task InputProducingTask(IDataReceiver inputStorage, System.Threading.CancellationToken cancellationToken)
        {
            while(TimeTable.TryPeek(out DateTime setTime) && setTime < DateTime.Now)
            {
                if(TimeTable.TryDequeue(out DateTime setTime1))
                {
                    //create an event and send to down-stream
                    inputStorage.Receive((new HandlerEvent(TimerId, setTime1)).ToRda());

                    Log($"Timer '{TimerId}' fired at {setTime1.ToString("HH:mm:ss")}.");
                }
            }

            //refill time-table when scheduled events have been exhausted
            if (TimeTable.Count == 0)
            {
                ResetTimeTable();
            }

            return Task.CompletedTask;
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