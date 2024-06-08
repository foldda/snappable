
namespace Foldda.DataAutomation.Framework
{
    //from most brief to most detailed
    public enum LoggingLevel : int { Info = 0, Detailed = 1, Verbose = 2, Debug = 3 };

    public interface ILoggingProvider 
    {
        LoggingLevel LoggingThreshold { get; set; }

        //proxy methods to the actually implement i.e. the FileLogger class
        void Log(string message, LoggingLevel loggingLevel);

        //proxy methods to the actually implement i.e. the FileLogger class
        void Log(string message);
    }
}