using Charian;
using Foldda.Automation.Framework;

using System;
using System.Collections.Generic;
using System.IO;

namespace Foldda.Automation.Trigger
{
    public class DailyTimer : BaseTimer
    {
        // a list of comma-separated digits (0=Sunday) indicating which day-of-week are excluded from emitting events 
        const string EXCLUDED_DAYS_OF_WEEK = "excluded-days-of-week"; 

        List<int> ExcludedWeekDays { get; set; } = new List<int>();

        public DailyTimer(ILoggingProvider logger) : base(logger) { }

        public override void SetParameters(IConfigProvider config)
        {
            base.SetParameters(config);

            string setting = config.GetSettingValue(EXCLUDED_DAYS_OF_WEEK, string.Empty);

            if(!string.IsNullOrEmpty(setting))
            {
                foreach (string token in setting.Split(new char[] { ',', ';'}))
                {
                    ExcludedWeekDays.Add(int.Parse(token));
                }
            }
        }

        internal override void ResetTimeTable()
        {
            Log("Daily timer events are scheduled as the following time - ");
            foreach (DayTimeSetting setting in TimeSettings)
            {
                DateTime settingTime = setting.DailyTime;

                if (settingTime > DateTime.Now && 
                    !ExcludedWeekDays.Contains((int)settingTime.DayOfWeek))
                {
                    TimeTable.Enqueue(settingTime);
                    Log(setting.HourlyTime.ToString("HH:mm:ss"));
                }
                //else, the specific time is already passed, don't add to time-table
            }
        }
    }
}
