using Foldda.DataAutomation.Framework;
using System;

namespace Foldda.DataAutomation.Util
{
    // a generic wrapper to a logging provider
    public class Logger : ILoggingProvider
    {
        public ILoggingProvider LoggingProvider { get; set; }

        public LoggingLevel LoggingThreshold { get => LoggingProvider.LoggingThreshold; set => LoggingProvider.LoggingThreshold = value; }

        //private Logger() { }    //make default constructor inaccessible

        public Logger(ILoggingProvider logger)
        {
            LoggingProvider = logger;
        }

        public void Log(Exception e)
        {
            if (e is AggregateException ae)
            {
                Log($"AggregateException - {ae.Message}");
                foreach (var ie in ae.Flatten().InnerExceptions)
                {
                    Log(ie);
                }
            }
            else
            {
                Log(e.GetFullMessage());    //use extension method https://stackoverflow.com/questions/5928976/what-is-the-proper-way-to-display-the-full-innerexception
            }
            Deb(e.StackTrace);
        }

        public void Log(string msg)
        {
            LoggingProvider?.Log(msg);
        }

        protected void Info(string v)
        {
            LoggingProvider?.Log(v, LoggingLevel.Info);
        }

        protected void Warn(string v)
        {
            LoggingProvider?.Log(v, LoggingLevel.Detailed);
        }

        protected void Err(string v)
        {
            LoggingProvider?.Log(v, LoggingLevel.Verbose);
        }

        protected void Deb(string msg)
        {
            LoggingProvider?.Log(msg, LoggingLevel.Debug);
        }

        public void Log(string message, LoggingLevel loggingLevel)
        {
            LoggingProvider?.Log(message, loggingLevel);
        }

        public virtual void LogCaller(string message)
        {
#if DEBUG
            Log($"[{this.GetType().Name}] {message}");
#else
            Log(message);
#endif
        }

    }
}
