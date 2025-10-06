using Foldda.Automation.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Util
{

    public class FileLogger : ILoggingProvider
    {
        private LogFile _logFile;

        public string LogFileName => _logFile.BaseName;

        public LoggingLevel LoggingThreshold { get; set; } = LoggingLevel.Debug;

 
        //key is the log file base-name, value is cached logging messages
        static ConcurrentDictionary<string, LogFile> LogFileCache = new ConcurrentDictionary<string, LogFile>();

        public FileLogger(string logFileBaseName) 
        {
            if (LogFileCache.TryGetValue(logFileBaseName, out LogFile cached) == true)
            {
                _logFile = cached;
            }
            else
            {
                _logFile = new LogFile(logFileBaseName);
                LogFileCache.TryAdd(_logFile.BaseName, _logFile);
            }
            timer = new Timer(new TimerCallback(this.FlushLogFile), _logFile, Timeout.Infinite, Timeout.Infinite); 
        }

        public FileLogger() : this(AppEnvironment.AssemblyDirectory)
        {
        }

        public static void LogByAssembly(string msg)
        {
            FileLogger logger = new FileLogger();   //default logging to assembly path
            logger.Log(msg);
        }

        public static void LogByAssembly(Exception e)
        {
            string msg = e.Message + "\n" + e.StackTrace;
            LogByAssembly(msg);
        }

        readonly int CACHE_LINES_LIMIT = 10;
        //static int cached_lines_count = 0;
        bool flushPending = false;

        Timer timer;

        public virtual void Log(string msg)
        {
            _Log(msg);
        }

        protected void _Log(string msg)
        {
            //cached_lines_count++;
            _logFile.CachedLines.Enqueue(msg);

            if (_logFile.CachedLines.Count <= CACHE_LINES_LIMIT && !flushPending)
            {
                timer.Change(150, Timeout.Infinite); //enable
                flushPending = true;
            }
            else
            {
                FlushLogFile(_logFile);
            }
        }


        private object spin = new object();

        private void FlushLogFile(object logFile)
        {
            lock (spin)
            {
                (logFile as LogFile).FlashCachedLines();
                flushPending = false;
            }
        }

        public void Flush()
        {
            FlushLogFile(_logFile);
        }

        public virtual void Log(string message, LoggingLevel messageLoggingLevel)
        {
            if (messageLoggingLevel <= LoggingThreshold)
            {
                _Log(message);
            }
        }


        //https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callermembernameattribute?view=net-8.0
        /// <summary>
        /// Usage: Trace("Something happened.");
        /// 
        /// The other parameters are supplied by the compiler, and do not need to be supplied.
        /// </summary>
        /// <param name="message">Your logging message.</param>
        /// <param name="memberName">supplied by the compiler</param>
        /// <param name="sourceFilePath">supplied by the compiler</param>
        /// <param name="sourceLineNumber">supplied by the compiler</param>
        public void Trace(string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            //to screen or debug console
            System.Diagnostics.Trace.WriteLine("message: " + message);
            System.Diagnostics.Trace.WriteLine("member name: " + memberName);
            System.Diagnostics.Trace.WriteLine("source file path: " + sourceFilePath);
            System.Diagnostics.Trace.WriteLine("source line number: " + sourceLineNumber);

            //to logger-specific media
            _Log("message: " + message);
            _Log("member name: " + memberName);
            _Log("source file path: " + sourceFilePath);
            _Log("source line number: " + sourceLineNumber);
        }


        class LogFile
        {
            internal string BaseName;   //without time stamp descr   $@"{_node.LogFolder}\{_node.ID}";
            private int LoggedLinesSinceLastRotate;
            internal ConcurrentQueue<string> CachedLines;
            const int ROTATE_LOG_SIZE = 500000; //the size thredthold (# of chars) for rotating the log files 
            private FileMode WriteMode = FileMode.Append;

            internal LogFile(string logFileBaseName)
            {
                BaseName = logFileBaseName;
                LoggedLinesSinceLastRotate = 0;
                CachedLines = new ConcurrentQueue<string>();
                WriteMode = FileMode.Append;
            }

            object spinLock = new object();
            internal void FlashCachedLines()
            {
                lock (spinLock)
                {
                    try
                    {
                        string logFileName = this.BaseName + System.DateTime.Now.ToString("_yyyyMMdd") + ".log";
                        //after rotating the log, content is cleared (FileMode.Truncate) before writing the next log line
                        //FileShare.ReadWrite is required for Tailing program to read the log file at the same time
                        using (var stream = new FileStream(logFileName, WriteMode, FileAccess.Write, FileShare.ReadWrite, bufferSize: 4096, useAsync: true))
                        //    new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                        //using (StreamWriter sw = new StreamWriter(stream) { AutoFlush = true })
                        {


                            while (CachedLines.TryDequeue(out string line))
                            {
                                string logLine = System.String.Format("[{0:T}] {1}\n", System.DateTime.Now, line).Replace("\n", Environment.NewLine);
                                this.LoggedLinesSinceLastRotate += logLine.Length;

                                //try a couple of times in case there are IO-exceptions (eg reading while writing)
                                for (int count = 0; count < 3; count++)
                                {
                                    try
                                    {
                                        byte[] bytes = System.Text.Encoding.Default.GetBytes(logLine);
                                        stream.Write(bytes, 0, bytes.Length);
                                        stream.Flush();
                                        //await stream.WriteAsync(bytes, 0, bytes.Length);
                                        //await stream.FlushAsync();

                                        //await sw.WriteLineAsync(logLine);

                                        break;  //no exception, done
                                    }
                                    catch// (IOException)
                                    {
                                        Task.Delay(200).Wait();
                                    }
                                }
                            }


                            //foreach (var line in this.CachedLines.GetConsumingEnumerable())
                            //{
                            //    string logLine = System.String.Print("[{0:T}] {1}\n", System.DateTime.Now, line).Replace("\n", Environment.NewLine);
                            //    this.LoggedLinesSinceLastRotate += logLine.Length;

                            //    //try a couple of times in case there are IO-exceptions (eg reading while writing)
                            //    for (int count = 0; count < 3; count++)
                            //    {
                            //        try
                            //        {
                            //            byte[] bytes = System.Text.Encoding.Default.GetBytes(logLine);
                            //            //await stream.WriteAsync(bytes, 0, bytes.Length);
                            //            //await stream.FlushAsync();
                            //            stream.Write(bytes, 0, bytes.Length);
                            //            stream.Flush();
                            //            break;  //no exception, done
                            //        }
                            //        catch// (IOException)
                            //        {
                            //            //await Task.Delay(200);
                            //        }
                            //    }
                            //}
                        }

                        //rotate log if it reaches certain size.
                        bool rotateLog = this.LoggedLinesSinceLastRotate >= ROTATE_LOG_SIZE;
                        if (rotateLog)
                        {
                            //Task.Delay(100).Wait();

                            string zipEntry = System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                            string rollupFile = logFileName + @"_ROLLUP.zip";
                            using (ZipArchive archive = ZipFile.Open(rollupFile, ZipArchiveMode.Update))
                            {
                                ZipArchiveEntry entry = archive.CreateEntryFromFile(logFileName, zipEntry);
                            }
                            this.LoggedLinesSinceLastRotate = 0;
                            WriteMode = FileMode.Truncate;
                        }
                        else
                        {
                            WriteMode = FileMode.Append;
                        }
                    }
                    catch (Exception e)
                    {
                        string i = e.Message;
                    }
                }
            }
        }
    }
}

