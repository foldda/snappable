using Charian;
using Foldda.Automation.Framework;
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
    public class OnOffSwitch : BasicDataHandler
    {
        public OnOffSwitch(ILoggingProvider logger) : base(logger) { }


        //this gets called when a node is started.
        public override void SetParameter(IConfigProvider config)
        {
            var now = DateTime.Now;
            string message = $"An event is triggered at {now.ToString("HH:mm:ss")} from source '{Id}'";
            //create an event and send to down-stream
            HandlerEvent record = new HandlerEvent(Id, now) { EventDetailsRda = new Rda() { ScalarValue = message } };
            OutputStorage.Receive(record);

            Log(message);
        }
    }
}