using Foldda.DataAutomation.Framework;
using System;
using System.IO;

namespace Foldda.DataAutomation.Timer
{
    //triggers an event at a pre-set (in seconds) interval
    public class SecondsTimer : BaseTimer
    {
        public const string SECONDS_INTERVAL = "seconds-interval";
        public SecondsTimer(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        private int SecondsInterval { get; set; }

        public override void SetParameters(IConfigProvider config)
        {
            base.SetParameters(config); //get timer-id, other settings are ignored

            string setting = config.GetSettingValue(SECONDS_INTERVAL, string.Empty);
            if(int.TryParse(setting, out int seconds) && seconds > 0)
            {
                SecondsInterval = seconds;
            }
            else
            {
                new Exception($"Invalid '{SECONDS_INTERVAL}' ({setting}) in settings, it must be a positive integer.");
            }

            ResetTimeTable();
        }

        internal override void ResetTimeTable()
        {
            var nextTime = DateTime.Now.AddSeconds(SecondsInterval);
            TimeTable.Enqueue(nextTime);
            Log($"Next scheduled time event is {nextTime.ToString("HH:mm:ss")}.");
        }
    }
}