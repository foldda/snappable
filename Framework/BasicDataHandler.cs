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
//using Foldda.OpenConnect.Framework;
//using Foldda.OpenConnect.Util;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// 
    /// This class implements the default ("do nothing") behavior for the three abstract tasks defined by the IDataProcessor:
    /// 
    /// 1) For gathering/generating input data of the data-processing chain (placing the produced data to the 'input storage')
    /// 2) For translating/transforming data based on the input from the 'input storage', and placing the result to the 'output storage'.
    /// 3) For dispatching processed data to an outside destination (eg database, file, network, etc)
    /// 
    /// The container stores parameters are supplied by a handler-hosting runtime environment, like Foldda (foldda.com).
    /// 
    /// </summary>
    public class BasicDataHandler : IDataHandler
    {
        public string Name { set; get; }

        public BasicDataHandler(ILoggingProvider logger)
        {
            Logger = logger;
        }

        //common config string
        public const string YES_STRING = "YES";
        public const string NO_STRING = "NO";

        public ILoggingProvider Logger { get; set; }

        public class FileReaderConfig : Rda
        {

            public enum RDA_INDEX : int { InputFileNameOrPattern, InputFilePath }

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
        }

        public virtual void SetParameters(IConfigProvider config) { }

        //public abstract Task ScanDirectory(DirectoryInfo directoryInfo, IDataReceiver handlerInputAggregatorReceiver, CancellationToken cancellationToken);


        /// <summary>
        /// Default is to run the node-folder file-scanning processor
        /// </summary>
        /// <param name="inputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task InputCollectingProcess(IDataContainerStore inputStorage, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        await InputProducingTask(inputStorage, cancellationToken);
                        await Task.Delay(1000);
                    } while (cancellationToken.IsCancellationRequested == false);
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException)
                    {
                        Log($"Handler '{this.GetType().Name}' operation is cancelled.");
                    }
                    else
                    {
                        Deb($"\nHandler operation is stopped unexpected due to error - {e.Message}.");
                        Log(e);
                        throw e;
                    }
                }
                finally
                {
                    Log($"Producing-input task stopped.");
                }

            });

            return;
        }

        public virtual async Task InputProducingTask(IDataContainerStore inputStorage, CancellationToken cancellationToken)
        {
            await Task.Delay(50);
        }

        /// <summary>
        /// This is a wrapper to the more simplified DataTransformationTask (method)
        /// </summary>
        /// <param name="inputStorage">Input records are from here</param>
        /// <param name="outputStorage">Result (output) records are stored here.</param>
        /// <param name="cancellationToken">Used to stop the processing loop.</param>
        /// <returns></returns>
        public virtual async Task InputProcessingProcess(IDataContainerStore inputStorage, IDataContainerStore outputStorage, CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        await DataTransformationTask(inputStorage, outputStorage, cancellationToken);
                    } while (cancellationToken.IsCancellationRequested == false) ;
                }
                catch (Exception e)
                {
                    if (e is OperationCanceledException)
                    {
                        Log($"Handler '{this.GetType().Name}' DataTransformationProcess operation is cancelled.");
                    }
                    else
                    {
                        Deb($"Handler data-transfomation process is stopped unexpected due to error - {e.Message}.");
                        throw e;
                    }
                }
                finally
                {
                    Log($"Handler data-transfomation processing task stopped.");
                }

            });
        }

        //take records from inputStorage, "processing" them, then result records are stored into the outputStorage.
        public virtual async Task DataTransformationTask(IDataContainerStore inputStorage, IDataContainerStore outputStorage, CancellationToken cancellationToken)
        {
            var input = inputStorage.CollectReceived();
            foreach (var inputContainer in input)
            {
                var outputContainer = ProcessContainer(inputContainer, cancellationToken);

                if (outputContainer.Records.Count > 0)
                {
                    outputStorage.Receive(outputContainer);
                }
            }

            await Task.Delay(50);
        }

        /// <summary>
        /// Check if the container is expected -  eg from the correct sender/source, if so, continue to process each record.
        /// </summary>
        /// <param name="inputContainer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A container of output/produced records</returns>
        protected virtual DataContainer ProcessContainer(DataContainer inputContainer, CancellationToken cancellationToken)
        {
            DataContainer outputContainer = new DataContainer() { MetaData = inputContainer.MetaData };
            //label the processed container .. default just keeping it as the input

            //process each record
            foreach (var record in inputContainer.Records)
            {
                ProcessRecord(record, inputContainer, outputContainer, cancellationToken);
            }

            return outputContainer;
        }

        /// <summary>
        /// Check is the record is expected. Each handler can expect one or more types of records to handle.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="outputContainer">This is where to deposite the produced (output) record if applicable.</param>
        /// <param name="cancellationToken"></param>
        public virtual void ProcessRecord(IRda record, DataContainer inputContainer, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            //default is a pass-through
            outputContainer.Add(record);
        }


        /// <summary>
        /// Implements the default behavior of consuming the output that are available in the output storage.
        /// 
        /// Sub-class override this method to implement output dispatching, eg. write to a file/database/network destination.
        /// </summary>
        /// <param name="outputStorage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task OutputDispatchingProcess(IDataContainerStore outputStorage, CancellationToken cancellationToken)
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        do
                        {
                            await OutputConsumingTask(outputStorage, cancellationToken);
                        } while (cancellationToken.IsCancellationRequested == false);
                    }
                    catch (Exception e)
                    {
                        if (e is OperationCanceledException)
                        {
                            Log($"Handler '{this.GetType().Name}' operation is cancelled.");
                        }
                        else
                        {
                            throw e;
                        }
                    }
                    finally
                    {
                        Log($"Consume-output task stopped.");
                    }

                });
            }

        public virtual async Task OutputConsumingTask(IDataContainerStore outputStorage, CancellationToken cancellationToken)
        {
            //the output is disregarded by default.
            _ = outputStorage.CollectReceived();

            await Task.Delay(50);   //delay is to avoid being a busy-loop
        }

        //Concrete sub-type handler to implement type-specific record-parser from char-stream (eg from a text file).
        //eg a HL7 message hander would provide a HL7 record parser here (see BaseHL7Handler class)
        public virtual AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            //used by 'sub-class dependent' (because of the record-type) file-scanning
            return null;    //by default 
        }

        protected void Deb(string v)
        {
            Logger?.Log(v, LoggingLevel.Debug);
        }

        //stores any file (full-names) that will be excluded from future scanning (such as files that contains no data)
        public HashSet<string> SkippedFileList { get; } = new HashSet<string>();
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
        protected static async Task<List<DataContainer>> ScanDirectory(
            DirectoryInfo targetDirectory, 
            string regexPattern, 
            ICollection<string> skippedFileList, 
            //IDataReceiver dataReceiver, 
            AbstractCharStreamRecordScanner scanner, 
            ILoggingProvider logger, 
            CancellationToken cancellationToken)
        {
            int total_count = 0;
            int scanned_files_count = 0;
            List<DataContainer> result = new List<DataContainer>();

            //check 
            if(targetDirectory == null || !targetDirectory.Exists)
            {
                logger?.Log($"Target-reading directory '{targetDirectory?.Name}' is null or invalid.");
                return result;
            }

            FileInfo[] files = targetDirectory.GetFiles().OrderBy(p => p.LastWriteTime).ToArray();
            foreach (FileInfo file in files)
            {
                if (skippedFileList.Contains(file.FullName))
                {
                    await Task.Delay(100);
                    continue;
                }
                else if (!Match(file.Name, regexPattern) && !file.Name.EndsWith(tempFileNameSuffix))
                {
                    skippedFileList.Add(file.FullName);
                    logger.Log($@"File [{file.Name}] in the target directory doesn't match the name-filter '{regexPattern}' and is skipped.");
                    continue;
                }

                //moving the file to a temp name to make sure it's not locked (eg being written)
                string newTempFileName = file.FullName;
                if (!file.Name.EndsWith(tempFileNameSuffix))
                {
                    newTempFileName = $"{file.FullName}{tempFileNameSuffix}";
                    try
                    {
                        logger.Log($"Moving {file.FullName} to {newTempFileName} before scanning.");
                        File.Move(file.FullName, newTempFileName);
                    }
                    catch (Exception e)
                    {
                        logger.Log($@"File [{file.Name}] is locked and cannot be processed. {e.Message}");
                        continue;   //to next file
                    }
                }

                try
                {
                    scanned_files_count++;
                    int count = 0;

                    Task producer = Task.Run(async () =>
                    {
                        using (Stream stream = new FileStream(newTempFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                           logger?.Log($"Input scanner is scanning file [{newTempFileName}] ... ");

                           await scanner.ScanRecordsInStreamAsync(stream, cancellationToken); //remember to end with bc.CompleteAdding();
                        } //stream-reading end
                    });


                    Task consumer = Task.Run(async () =>
                    {
                        //this task harvests the scanner-produced records
                        DataContainer container = new DataContainer() { MetaData = new Rda() { ScalarValue = file.Name } };

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            if (scanner.HarvestedRecords.TryTake(out char[] recordChars))
                            {
                                IRda record = scanner.Parse(recordChars, Encoding.Default);
                                //dataReceiver.Receive(record.ToRda());
                                container.Add(record);
                                count++;
                            }
                            else if (scanner.HarvestedRecords.IsCompleted)
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

                    logger.Log($"waiting for scanner tasks to complete ...");

                    await Task.WhenAll(consumer, producer);

                    //before exiting the scanning, we check if we shall delete the processed data
                    //if the files contains no legit data, we don't delete the file but put it in a black-list
                    if (count == 0)
                    {
                        File.Move(newTempFileName, file.FullName);
                        logger.Log($"Restored {newTempFileName} to {file.FullName} and will not attempt to process it until next restart.");
                        //if no records found, exclude this file (but don't delete) in the future scanning 
                        skippedFileList.Add(file.FullName);
                    }
                    else
                    {
                        File.Delete(newTempFileName);
                        logger.Log($@"File [{newTempFileName}] is deleted after {count} records being retrieved.");
                    }

                    //end of scanning the folder
                    logger.Log($"file scanner producer/consumer tasks completed");
                    total_count += count;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.Log(e.ToString());
                }



            } // loop-end of directory files iteration 

            if(scanned_files_count > 0)
            {
                logger.Log($"Folder '{targetDirectory.Name}' scanning task completed with total {scanned_files_count} files scanned and {total_count} records collected");
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
    }
}

