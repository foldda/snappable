using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using System;
using System.IO;
using System.Linq;

namespace Foldda.Automation.Trigger
{
    //repeat a number of events within an hour
    public class HourlyTimer : BaseTimer
    {
        public HourlyTimer(IHandlerManager manager) : base(manager) { }

        internal override void ResetTimeTable()
        {
            DateTime nowTime = DateTime.Now;

            Log("Hourly timer events are scheduled as the following time - ");
            foreach (DayTimeSetting setting in TimeSettings)
            {
                if (setting.HourlyTime > nowTime)
                {
                    if (!TimeTable.Contains(setting.HourlyTime))
                    {
                        TimeTable.Enqueue(setting.HourlyTime);
                        Log(setting.HourlyTime.ToString("mm:ss"));
                    }
                }
                //else, the specific time is already passed, don't add to time-table
            }
        }
    }
}
