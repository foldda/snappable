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
        public CsvFileReader(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }
        public override void SetParameters(IConfigProvider config)
        {
            base.SetParameters(config); //constructs the RecordEncoding

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

        protected override void ProcessEvent(HandlerEvent event1, DataContainer inputContainer, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            try
            {
                //testing if the trigger contains 'file-read config instructions' in its context,
                if(!(event1.EventDetailsRda is FileReaderConfig fileReaderConfig))
                {
                    //if not, use the handler's local settings
                    Log($"Container has no file-download instrcution, local (FTP) config settings are used.");
                    fileReaderConfig = LocalConfig;
                }

                ReadFileTask(fileReaderConfig.InputFilePath, fileReaderConfig.InputFileNameOrPattern, outputContainer, cancellationToken);

                Log($"Read {outputContainer.Records.Count} records.");
            }
            catch (Exception e)
            {
                Log(e);
                throw e;
            }
        }

        private Task ReadFileTask(string CsvSourceFolderPath, string SourceFileNamePattern, DataContainer outputContainer, CancellationToken cancellationToken)
        {
            DirectoryInfo targetDirectory = new DirectoryInfo(CsvSourceFolderPath);

            var result = ScanDirectory(targetDirectory, SourceFileNamePattern, SkippedFileList, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;

            foreach (var container in result)
            {
                TabularRecord.MetaData metaData = new TabularRecord.MetaData() { SourceId = container.MetaData.ToRda().ScalarValue };
                if(container.Records.Count > 0)
                {
                    if(FirstLineIsHeader == true)
                    {
                        var headerLine = container.Records.First();
                        metaData.ColumnNames = headerLine.ToRda().ChildrenValueArray;
                        container.Records.RemoveAt(0);
                    }

                    outputContainer.MetaData = metaData;

                    outputContainer.Records.AddRange(container.Records);
                }
            }
            return Task.CompletedTask;
        }
    }
}
