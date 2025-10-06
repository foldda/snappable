using Foldda.Automation.Framework;
using System;
using System.IO;

namespace Foldda.Automation.Trigger
{
    //triggers an event at a pre-set (in seconds) interval
    public class SecondsTimer : BaseTimer
    {
        public const string SECONDS_INTERVAL = "seconds-interval";
        public SecondsTimer(IHandlerManager manager) : base(manager) { }

        private int SecondsInterval { get; set; }

        public override void Setup(IConfigProvider config)
        {
            base.Setup(config); //get timer-id, other settings are ignored

            string setting = config.GetSettingValue(SECONDS_INTERVAL, string.Empty);
            if(int.TryParse(setting, out int seconds) && seconds > 0)
            {
                SecondsInterval = seconds;
            }
            else
            {
                new Exception($"Invalid '{SECONDS_INTERVAL}' value ({setting}) in settings, it must be a positive integer.");
            }

            _lastAddedTime = DateTime.Now;
            ResetTimeTable();
        }

        DateTime _lastAddedTime;

        internal override void ResetTimeTable()
        {
            //don't add new entry if there is a fire time pending
            if (TimeTable.TryPeek(out DateTime nextFireTime) && nextFireTime > DateTime.Now)
            {
                _lastAddedTime = nextFireTime;  
            }
            else
            { 
                //add new entry and ensure it's in the future
                var nextTime = _lastAddedTime.AddSeconds(SecondsInterval);
                if(nextTime < DateTime.Now)
                {
                    nextTime = DateTime.Now.AddSeconds(SecondsInterval);
                }

                TimeTable.Enqueue(nextTime);
                _lastAddedTime = nextTime;            
                Log($"Next scheduled time event is {nextTime.ToString("HH:mm:ss")}.");
            }

        }
    }
}