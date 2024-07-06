using Charian;
using System;
using System.Collections.Generic;
using System.Text;

namespace Foldda.Automation.Util
{
    //a time object that can be store in Rda
    public class TimeRda : IRda
    {
        public DateTime Value { get; private set; }

        public TimeRda(DateTime dateTime)
        {
            Value = dateTime;
        }

        public TimeRda(Rda timeRda)
        {
            FromRda(timeRda);
        }

        const int TRUNCATE_FACTOR = 1000000;    //make the stored value shorter

        //Rda stores a (truncated) 1/1m "ticks" value of a DateTime value
        public IRda FromRda(Rda rda)
        {
            //restores the original ticks value (multiplies the FACTOR), then get the actual time value
            Value = new DateTime(long.Parse(rda.ScalarValue) * TRUNCATE_FACTOR);   
            return this;
        }

        public Rda ToRda()
        {
            //divid by 1m to shorten the string length (also will reduce the time resolution)
            var rda = new Rda() { ScalarValue = (Value.Ticks / TRUNCATE_FACTOR).ToString() };
            return rda;
        }

    }
}
