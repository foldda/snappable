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
        enum State : int { ON, OFF }

        State _state = State.OFF;

        string TriggerId => Id;

        public OnOffSwitch(ILoggingProvider logger) : base(logger) { }

        public override Task ProcessData(CancellationToken cancellationToken)
        {
            if(_state == State.OFF)
            {
                var now = DateTime.Now;
                //create an event and send to down-stream
                HandlerEvent record = new HandlerEvent(TriggerId, now) { EventDetailsRda = new Rda() { ScalarValue = "Switch is turned ON." } };
                OutputStorage.Receive(record);

                _state = State.ON;
                Log($"Switch '{TriggerId}' is turned ON at {now.ToString("HH:mm:ss")}.");
            }

            return Task.CompletedTask;
        }

        //this gets called when a node is started.
        public override void SetParameter(IConfigProvider config)
        {
            _state = State.OFF;
        }
    }
}