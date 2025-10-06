using Charian;
using Foldda.Automation.Framework;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.CsvHandler
{
    public class CsvFileReader : BaseCsvHandler
    {

        public const string INPUT_FILE_NAME_PATTERN = "input-file-name-pattern";
        public const string INPUT_FILE_PATH = "csv-input-path";

        internal FileReaderConfig LocalConfig { get; private set; }
        public CsvFileReader(IHandlerManager manager) : base(manager) { }
        public override void Setup(IConfigProvider config)
        {
            base.Setup(config); //constructs the RecordEncoding

            LocalConfig = new FileReaderConfig()
            {
                InputFileNameOrPattern = config.GetSettingValue(INPUT_FILE_NAME_PATTERN, string.Empty),
                InputFilePath = config.GetSettingValue(INPUT_FILE_PATH, string.Empty)
            };

            var paramSourcePath = LocalConfig.InputFilePath;
            if (string.IsNullOrEmpty(paramSourcePath) || !Directory.Exists(paramSourcePath))
            {
                Log($"ERROR - '{INPUT_FILE_PATH}' parameter in setting '{paramSourcePath}' is invalid.");
            }

            if (string.IsNullOrEmpty(LocalConfig.InputFilePath))
            {
                Log($"ERROR - parameter '{INPUT_FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
            }
        }

        /// <summary>
        /// In this case, the handler reponds to its manager-injected timer "heart-beat" event and performs a file-reading task using the default config.
        /// </summary>
        /// <param name="handlerEvent">an event</param>
        /// <param name="cancellationToken">optionally cancels async ops</param>
        /// <returns>a task can be waited upon</returns>
        protected override Task ProcessHandlerEvent(MessageRda.HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            //manger-injected "heart-beat" timer event has EventDetailsRda = NULL
            if (handlerEvent.EventSourceId.Equals(HandlerManager.UID) && handlerEvent.EventDetailsRda == Rda.NULL)
            {   
                return ReadFileTask(LocalConfig.InputFilePath, LocalConfig.InputFileNameOrPattern, cancellationToken);
            }
            else
            {
                Logger.Log($"WARNING - Event is not handled - {handlerEvent.EventSourceId}: {handlerEvent.EventDetailsRda}"); ;
                return Task.Delay(50);
            }
        }

        /// <summary>
        /// The handler also respond to incoming notification messages that carry FileReaderConfig as the message body
        /// </summary>
        /// <param name="handlerNotification">an incoming message</param>
        /// <param name="cancellationToken">optionally cancels async ops</param>
        /// <returns>a task can be waited upon</returns>
        protected override Task ProcessHandlerNotification(MessageRda.HandlerNotification handlerNotification, CancellationToken cancellationToken)
        {
            //force sub-class to implement
            if (handlerNotification.ReceiverId.Equals(this.UID) && handlerNotification.NotificationBodyRda is FileReaderConfig readConfig)
            {
                return ReadFileTask(readConfig.InputFilePath, readConfig.InputFileNameOrPattern,cancellationToken);
            }
            else
            {
                Logger.Log($"WARNING - Notification message is not handled - {handlerNotification.SenderId}: {handlerNotification.ReceiverId} : {handlerNotification.NotificationBodyRda}"); ;
                return Task.Delay(50);
            }
        }


        private Task ReadFileTask(string CsvSourceFolderPath, string SourceFileNamePattern, CancellationToken cancellationToken)
        {
            try
            {
                DirectoryInfo targetDirectory = new DirectoryInfo(CsvSourceFolderPath);

                var result = ScanDirectory(targetDirectory, SourceFileNamePattern, DefaultFileRecordScanner, Logger, cancellationToken).Result;

                foreach (var container in result)
                {
                    TabularRecord.MetaData metaData = new TabularRecord.MetaData() { SourceId = container.MetaData.ToRda().ScalarValue };
                    Log($"Read {container.Records.Count} records.");
                    if (container.Records.Count > 0)
                    {
                        if (FirstLineIsHeader == true)
                        {
                            var headerLine = container.Records.First();
                            metaData.ColumnNames = headerLine.ToRda().ChildrenValueArray;
                            container.Records.RemoveAt(0);
                        }

                        container.MetaData = metaData;
                        HandlerManager.PipelineOutputDataStorage.Receive(container);
                    }
                }

            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Task.Delay(5000).Wait();
            }

            return Task.CompletedTask;
        }
    }
}
