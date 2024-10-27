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
        public CsvFileReader(ILoggingProvider logger) : base(logger) { }
        public override void SetParameter(IConfigProvider config)
        {
            base.SetParameter(config); //constructs the RecordEncoding

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

        protected override Task ProcessHandlerEvent(HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            try
            {
                //testing if the trigger contains 'file-read config instructions' in its context,
                if (!(handlerEvent.EventDetailsRda is FileReaderConfig fileReaderConfig))
                {
                    //if not, use the handler's local settings
                    Log($"Input event has no file-reading instruction, local config settings are used.");
                    fileReaderConfig = LocalConfig;
                }

                ReadFileTask(fileReaderConfig.InputFilePath, fileReaderConfig.InputFileNameOrPattern, cancellationToken);
            }
            catch (Exception e)
            {
                Log(e);
                throw e;
            }

            return Task.Delay(50);
        }

        private Task ReadFileTask(string CsvSourceFolderPath, string SourceFileNamePattern, CancellationToken cancellationToken)
        {
            try
            {
                DirectoryInfo targetDirectory = new DirectoryInfo(CsvSourceFolderPath);

                var result = ScanDirectory(targetDirectory, SourceFileNamePattern, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;

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
                        OutputStorage.Receive(container);
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
