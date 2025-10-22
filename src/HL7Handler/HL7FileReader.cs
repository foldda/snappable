using Charian;
using Foldda.Automation.Framework;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Foldda.Automation.HL7Handler.HL7NetReceiver;

namespace Foldda.Automation.HL7Handler
{
    public class HL7FileReader : BaseHL7Handler
    {
        public HL7FileReader(ISnappableManager manager) : base(manager) { }

        const string FILE_NAME_PATTERN = "file-name-pattern";
        const string SOURCE_PATH = "source-path";
        const string PATH_SCANNING_INTERVAL_SEC = "scanning-interval-sec";

        //protected string TargetFileNamePattern { get; private set; }
        //protected string SourcePath { get; private set; }

        protected FileReaderConfig DefaultFileReaderConfig { get; private set; }

        public override void Setup(IConfigProvider config)
        {
            DefaultFileReaderConfig = null;

            string SourcePath = config.GetSettingValue(SOURCE_PATH, string.Empty);
            if (string.IsNullOrEmpty(SourcePath) || !Directory.Exists(SourcePath))
            {
                Log($"ERROR - supplied path '{SourcePath}' does not exist.");
            }
            else
            {
                string paramFileNamePattern = config.GetSettingValue(FILE_NAME_PATTERN, string.Empty);
                if (string.IsNullOrEmpty(paramFileNamePattern))
                {
                    Log($"ERROR - parameter '{FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
                }
                else
                {
                    //parameters checked OK
                    DefaultFileReaderConfig = new FileReaderConfig()
                    {
                        InputFilePath = SourcePath,
                        InputFileNameOrPattern = paramFileNamePattern,
                        FilePathScanningIntervalSec = config.GetSettingValue(PATH_SCANNING_INTERVAL_SEC, 1)
                    };
                }
            }
        }

        public override Task<int> Init(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    if(DefaultFileReaderConfig != null)
                    {
                        Log("Starting automatic path scanning for source files reading ...)");
                        Task dataCollectorTask = Task.Run(async () =>
                        {
                            try
                            {
                                do
                                {
                                    await ScanHL7Data(DefaultFileReaderConfig, cancellationToken);

                                    await Task.Delay(1000 * DefaultFileReaderConfig.FilePathScanningIntervalSec);

                                } while (cancellationToken.IsCancellationRequested == false);
                            }
                            catch (Exception e)
                            {
                                Log(e);
                            }

                        });

                        Task.WaitAll(dataCollectorTask);
                    }
                    else
                    {
                        Log("File reading config not found or is invalid, reading will be by triggering (no automatic scanning)");
                    }
                }
                catch (Exception e)
                {
                    Log($"\nHandler Init task is stopped due to exception - {e.Message}.");
                }
                finally
                {
                    //Node.LogEvent(Constant.NodeEventType.LastStop);
                    //don't set STATE here, let command and state-table to drive state 
                    Log($"Node handler '{this.GetType().Name}' Init task completed.");
                }

                return 0;

            });
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
                return ScanHL7Data(readConfig, cancellationToken);
            }
            else
            {
                Logger.Log($"WARNING - Notification message is not handled - {handlerNotification.SenderId}: {handlerNotification.ReceiverId} : {handlerNotification.NotificationBodyRda}"); ;
                return Task.Delay(50);
            }
        }


        private Task ScanHL7Data(FileReaderConfig readConfig, CancellationToken cancellationToken)
        {
            try
            {
                DirectoryInfo targetDirectory = new DirectoryInfo(readConfig.InputFilePath);

                var result = ScanDirectory(targetDirectory, readConfig.InputFileNameOrPattern, DefaultFileRecordScanner, Logger, cancellationToken).Result;
                foreach (var container in result)
                {
                    Manager.PipelineOutputDataStorage.Receive(container);
                    Log($"Added 1 container having {container.Records.Count} records to the output pipeline.");
                }
            }
            catch(Exception ex) 
            {
                Log(ex.Message);
                Task.Delay(5000).Wait();
            }
            return Task.CompletedTask;
        }

    }
}
