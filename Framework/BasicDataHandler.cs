using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using Charian;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Data;
using System.Runtime.CompilerServices;
//using Foldda.OpenConnect.Framework;
//using Foldda.Automation.;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// 
    /// This class implements the default (no-op, "do nothing") behavior for the 1 abstract task defined by the IDataHandler:
    /// 
    /// 2) For translating/transforming data based on the inboundMessages from the 'inboundMessages storage', and placing the result to the 'output storage'.
    /// 
    /// The inputContainer stores parameters are supplied by a hosting runtime environment, like Foldda from foldda.com.
    /// 
    /// </summary>
    public class BasicDataHandler : IDataHandler
    {
        public string UID => HandlerManager.UID;
        public string Name => HandlerManager.Name;

        public BasicDataHandler(IHandlerManager handlerManager)
        {
            HandlerManager = handlerManager;
        }

        //common config string
        public const string YES_STRING = "YES";
        public const string NO_STRING = "NO";

        public IHandlerManager HandlerManager { get; set; }

        public ILoggingProvider Logger => HandlerManager.HandlerLogger;

        public class FileReaderConfig : Rda
        {
            public enum RDA_INDEX : int { InputFileNameOrPattern, InputFilePath, FilePathScanningIntervalSec }

            public string InputFileNameOrPattern
            {
                get => this[(int)RDA_INDEX.InputFileNameOrPattern].ScalarValue;
                set => this[(int)RDA_INDEX.InputFileNameOrPattern].ScalarValue = value.ToString();
            }
            public string InputFilePath
            {
                get => this[(int)RDA_INDEX.InputFilePath].ScalarValue;
                set => this[(int)RDA_INDEX.InputFilePath].ScalarValue = value.ToString();
            }

            const int DEFAULT_INTERVAL_SEC = 1;

            public int FilePathScanningIntervalSec
            {
                get => int.TryParse(this[(int)RDA_INDEX.FilePathScanningIntervalSec].ScalarValue, out int result) ? result : DEFAULT_INTERVAL_SEC;
                set => this[(int)RDA_INDEX.FilePathScanningIntervalSec].ScalarValue = value.ToString();
            }
        }

        public virtual void Setup(IConfigProvider config) 
        {
            var now = DateTime.Now;
            string message = $"EVENT: Handler '{Name}' is started at {now.ToString("HH:mm:ss")}.";
            //create an event and send to down-stream
            MessageRda record = new MessageRda.HandlerEvent(UID, now, new Rda() { ScalarValue = message });
            HandlerManager.PostHandlerOutboundMessage(record);

            Log(message);

        }

        /// <summary>
        /// Process a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any, to a designated storage from the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        public virtual Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken)
        {
            HandlerManager.PipelineOutputDataStorage.Receive(inputContainer);    //default is pass-thru

            ///alternatively processing each record indivisually ... something like

            //foreach (var record in inputContainer.Records)
            //{
            //    IRda output = ProcessRecord(record);
            //    HandlerManager.PipelineOutputDataStorage.Receive(output);
            //}

            return Task.FromResult(0);
        }


        /// <summary>
        /// Process a handler message - passed in by the handler manager.
        /// Note this handler would deposite its output, if any , to designated storage(s) via the manager
        /// </summary>
        /// <param name="message">a handler message, can be an event, notification, or command, or other types</param>
        /// <returns>a status integer</returns>
        /// <param name="cancellationToken"></param>
        public virtual Task<int> ProcessInboundMessage(MessageRda message, CancellationToken cancellationToken)
        {
            if (message is MessageRda.HandlerEvent handlerEvent)
            {
                ProcessHandlerEvent(handlerEvent, cancellationToken);
            }
            else if (message is MessageRda.HandlerNotification handlerNotification)
            {
                ProcessHandlerNotification(handlerNotification, cancellationToken);
            }
            return Task.FromResult(0);
        }


        //in this virtual method, the event is processed and may produce an event being posted to the handler-manager's message-board
        //or a new data inputContainer to the OutputStoarge.
        protected virtual Task ProcessHandlerEvent(MessageRda.HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            //force sub-class to implement
            if(handlerEvent.EventSourceId.Equals(HandlerManager.UID))
            {
                //by default, ignore Handler Manager's injected events - which is from Runtime
                //TODO - processing Handler Manager events here..
            }
            else
            {
                //Log($"WARNING - FrameworkMessage from upstream is not handled - {handlerEvent.EventSourceId}: {handlerEvent.EventDetailsRda}"); ;
            }
            //throw new NotImplementedException();
            return Task.Delay(50);
        }

        protected virtual Task ProcessHandlerNotification(MessageRda.HandlerNotification handlerNotification, CancellationToken cancellationToken)
        {
            //force sub-class to implement
            if (handlerNotification.ReceiverId.Equals(this.UID)) 
            {
                //by default, ignore Handler Manager's injected events - which is from Runtime
                //some handlers, such as the HL7FileReader/CsvFileReader, override this method and leverage these 'heartbeat' events to trigger routine directory scanning/file reading
                if(handlerNotification.NotificationBodyRda == Rda.NULL)
                {
                    //runtime timer event
                }
                else
                {
                    //TODO - processing Handler Manager events here..
                }
            }
            else
            {
                Log($"WARNING - Notification message is not handled - {handlerNotification.SenderId}: {handlerNotification.NotificationBodyRda}"); ;
            }
            //throw new NotImplementedException();
            return Task.Delay(50);
        }

        protected virtual AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            //used by 'sub-class-dependent' (because of the record-type) encoding file-scanning eg file reading and archiving 
            return null;    //by default 
        }

        protected AbstractCharStreamRecordScanner _defaultFileRecordScanner;

        public virtual AbstractCharStreamRecordScanner DefaultFileRecordScanner 
        {
            get
            { 
                if(_defaultFileRecordScanner == null)
                {
                    _defaultFileRecordScanner = GetDefaultFileRecordScanner(Logger);
                }
                return _defaultFileRecordScanner; 
            }
        }

        protected void Deb(string v)
        {
            Logger?.Log(v, LoggingLevel.Debug);
        }


        private static bool Match(string strValue, string regexPattern)
        {
            if(string.IsNullOrEmpty(strValue)) { return false; }
            else if(string.IsNullOrEmpty(regexPattern)) { return true; }
            else
            {
                string dosConverted = regexPattern;
                //https://stackoverflow.com/questions/20960313/how-to-implement-msdos-style-pattern-matching-wildcards
                if (!regexPattern.StartsWith("^") && !regexPattern.EndsWith("$"))
                {
                    dosConverted = "^" + regexPattern.Replace(".", "\\.").Replace("*", ".*").Replace('?', '.') + "$";
                }

                try
                {
                    Regex regex = new Regex(dosConverted);
                    return regex.IsMatch(strValue);
                }
                catch
                {
                    new Exception($"Regex-pattern '{regexPattern}' is invalid.");
                    return false;
                }
            }
        }

        const string tempFileNameSuffix = "$$FolddaTmp$$";  //for "moving-aside" a file before reading
        public static async Task<List<RecordContainer>> ScanDirectory(
            DirectoryInfo targetDirectory, 
            string regexPattern, 
            //ICollection<string> skippedFileList, 
            //IDataReceiver dataReceiver, 
            AbstractCharStreamRecordScanner scanner, 
            ILoggingProvider logger, 
            CancellationToken cancellationToken)
        {
            int total_count = 0;
            int scanned_files_count = 0;
            List<RecordContainer> result = new List<RecordContainer>();

            //check 
            if(targetDirectory == null || !targetDirectory.Exists)
            {
                logger?.Log($"Target-reading directory '{targetDirectory?.Name}' is null or invalid.");
                return result;
            }

            var files = targetDirectory.GetFiles().OrderBy(p => p.LastWriteTime);
            foreach (FileInfo file in files)
            {
                if (scanner.SkippedFileList.Contains(file.FullName))
                {
                    await Task.Delay(100);
                    continue;
                }
                else if (!Match(file.Name, regexPattern) && !file.Name.EndsWith(tempFileNameSuffix))
                {
                    scanner.SkippedFileList.Add(file.FullName);
                    logger?.Log($@"File [{file.Name}] in directory '{targetDirectory.Name}' is skipped from future scanning as it doesn't match the targeted name pattern '{regexPattern}'.");
                    continue;
                }

                //moving the file to a temp name to make sure it's not locked (eg being written)
                string newTempFileName = file.FullName;
                if (!file.Name.EndsWith(tempFileNameSuffix))
                {
                    newTempFileName = $"{file.FullName}{tempFileNameSuffix}";
                    try
                    {
                        logger?.Log($"Moving {file.FullName} to {newTempFileName} before scanning.");
                        File.Move(file.FullName, newTempFileName);
                    }
                    catch (Exception e)
                    {
                        logger?.Log($@"File [{file.Name}] is locked and cannot be processed. {e.Message}");
                        continue;   //to next file
                    }
                }

                try
                {
                    scanned_files_count++;
                    int count = 0;
                    bool producerCompleted = false;

                    Task producer = Task.Run(async () =>
                    {
                        using (Stream stream = new FileStream(newTempFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                           logger?.Log($"Input scanner is scanning file [{newTempFileName}] ... ");

                           await scanner.ScanRecordsInStreamAsync(stream, cancellationToken); //remember to end with bc.CompleteAdding();
                           producerCompleted = true;
                        } //stream-reading end
                    });


                    Task consumer = Task.Run(async () =>
                    {
                        //this task harvests the scanner-produced records
                        RecordContainer container = new RecordContainer() 
                        { 
                            MetaData = new RecordContainer.DefaultMetaData(file.Name, DateTime.UtcNow), 
                            RecordEncoding = scanner.RecordEncoding 
                        };

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            if (scanner.HarvestedRecords.TryTake(out char[] recordChars))
                            {
                                IRda record = scanner.Parse(recordChars, Encoding.Default);
                                //dataReceiver.Receive(record.ToRda());
                                container.Add(record);
                                count++;
                            }
                            else if (producerCompleted)
                            {
                                break;
                            }
                            else
                            {
                                await Task.Delay(100);
                            }
                        }

                        result.Add(container);

                    }, cancellationToken);

                    logger?.Log($"waiting for scanner tasks to complete ...");

                    await Task.WhenAll(consumer, producer);

                    //before exiting the scanning, we check if we shall delete the processed data
                    //if the files contains no legit data, we don't delete the file but put it in a black-list
                    if (count == 0)
                    {
                        File.Move(newTempFileName, file.FullName);
                        logger?.Log($"Restored {newTempFileName} to {file.FullName}, which contains no legit records, and will not attempt to process it until next restart.");
                        //if no records found, exclude this file (but don't delete) in the future scanning 
                        scanner.SkippedFileList.Add(file.FullName);
                    }
                    else
                    {
                        File.Delete(newTempFileName);
                        logger?.Log($@"File [{newTempFileName}] is deleted after {count} records being retrieved.");
                    }

                    //end of scanning the folder
                    logger?.Log($"file scanner producer/consumer tasks completed");
                    total_count += count;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger?.Log(e.ToString());
                }



            } // loop-end of directory files iteration 

            if(scanned_files_count > 0)
            {
                logger?.Log($"Folder '{targetDirectory.Name}' scanning task completed with total {scanned_files_count} files scanned and {total_count} records collected");
                scanned_files_count = 0;
                total_count = 0;
            }

            return result;
        }

        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static void Log(ILoggingProvider logger, string message)
        {
            logger?.Log(message);
        }

        public void Log(string message)
        {
            Log(Logger, message);
        }

        public void Log(Exception e)
        {
            Log(Logger, e.ToString());
        }

        public virtual Task<int> Init(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);  //default
        }

        public class DictionaryRda : IRda
        {
            public Dictionary<string, Rda> Store { get; } = new Dictionary<string, Rda>();

            public DictionaryRda() { }

            public DictionaryRda(Rda rda)
            {
                FromRda(rda);
            }

            public bool TryGetString(string key, out string value)
            {
                bool result = TryGetRda(key, out Rda rda);
                value = rda?.ScalarValue ?? null;
                return result;
            }

            public bool TryGetRda(string key, out Rda output)
            {
                bool result = Store.TryGetValue(key, out Rda rda);
                output = rda;
                return result;
            }

            public string GetString(string key)
            {
                return TryGetString(key, out string result) ? result : null;
            }

            public Rda GetRda(string key)
            {
                return TryGetRda(key, out Rda result) ? result : null;
            }

            public void SetString(string key, string value)
            {
                SetRda(key, new Rda() { ScalarValue = value });
            }

            public void SetRda(string key, Rda rda)
            {
                if (Store.ContainsKey(key)) 
                { 
                    Store.Remove(key); //remove existing key otherwise Dictionary will trhow Exception
                }

                Store.Add(key, rda);
            }

            //Rda stores a (truncated) 1/1m "ticks" value of a DateTime value
            public IRda FromRda(Rda rda)
            {
                //restores the original ticks value (multiplies the FACTOR), then get the actual time value
                Store.Clear();
                foreach (Rda item in rda.Elements)
                {
                    Pair pair = new Pair(item);
                    Store.Add(pair.Name, pair.Value);
                }
                return this;
            }

            public Rda ToRda()
            {
                //divid by 1m to shorten the string length (also will reduce the time resolution)
                Rda rda = new Rda();
                foreach (string key in Store.Keys)
                {
                    rda.Elements.Add(new Pair(key, Store[key]));
                }
                return rda;
            }

            class Pair : Rda
            {
                internal Pair(Rda rda) : base()
                {
                    FromRda(rda);
                }
                internal Pair(string key, IRda value) : base()
                {
                    Name = key;
                    Value = value.ToRda();
                }

                //internal Pair(string key, string value) : this(key, Parse(value)) { }

                enum META_DATA : int { NAME, VALUE } //

                public string Name   //
                {
                    get => this[(int)META_DATA.NAME].ScalarValue;
                    set => this[(int)META_DATA.NAME].ScalarValue = value;
                }

                public Rda Value   // 
                {
                    get => this[(int)META_DATA.VALUE];
                    set => this[(int)META_DATA.VALUE] = value;
                }
            }

        }
    }
}

