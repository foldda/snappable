using Charian;
using Foldda.DataAutomation.Framework;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.CsvHandler
{
    public class CsvFileReader : BaseCsvHandler
    {
        const string INPUT_FILE_NAME_PATTERN = "input-file-name-pattern";
        const string CSV_INPUT_PATH = "csv-input-path";

        protected string SourceFileNamePattern { get; private set; }
        protected string CsvSourceFolderPath { get; private set; }
        public CsvFileReader(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }
        public override void SetParameters(IConfigProvider config)
        {
            base.SetParameters(config); //constructs the RecordEncoding

            var paramSourcePath = config.GetSettingValue(CSV_INPUT_PATH, string.Empty);
            if (string.IsNullOrEmpty(paramSourcePath))
            {
                Log($"ERROR - parameter '{CSV_INPUT_PATH}' is mandatory and it's not supplied.");
            }
            else if (!Directory.Exists(paramSourcePath))
            {
                Log($"ERROR - supplied path '{paramSourcePath}' does not exist.");
            }
            else
            {
                CsvSourceFolderPath = paramSourcePath;
            }

            var paramFileName = config.GetSettingValue(INPUT_FILE_NAME_PATTERN, string.Empty);
            if (string.IsNullOrEmpty(paramFileName))
            {
                Log($"ERROR - parameter '{INPUT_FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
            }

            //parameters checked OK
            SourceFileNamePattern = paramFileName;
        }

        public override Task InputProducingTask(IDataReceiver inputStorage, CancellationToken cancellationToken)
        {
            DirectoryInfo targetDirectory = new DirectoryInfo(CsvSourceFolderPath);

            var result = ScanDirectory(targetDirectory, SourceFileNamePattern, SkippedFileList, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;

            foreach (var container in result)
            {
                TabularRecord.MetaData metaData = new TabularRecord.MetaData() { SourceId = container.MetaData.ScalarValue };
                if(container.Records.Count > 0)
                {
                    if(FirstLineIsHeader == true)
                    {
                        var headerLine = container.Records.First();
                        metaData.ColumnNames = headerLine.ChildrenValueArray;
                        container.Records.RemoveAt(0);
                    }

                    container.MetaData = metaData;

                    inputStorage.Receive(container);
                }
            }
            return Task.CompletedTask;
        }
    }
}
