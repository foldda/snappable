using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foldda.Automation.CsvHandler;
using Foldda.Automation.Framework;

namespace MyCompany.MyApp.MyCustomHandler
{
    /// <summary>
    /// MyCsvHandler is an exact copy of the CsvFileReader handler, so if compiled as it is, it will be your "own (file-reader) handler" that does the same function, except it will reside in a different DLL
    /// which under your control. In other words, from here you can change this handler to do whatever you want, and embed this whatever feature to the DLL to be used in your own handler.
    /// For example, you can make this handler to read input files simutanously from multiple locations, or to do something entirely irrelavent to file-reading.
    /// </summary>
    public class MyCsvHandler : BaseCsvHandler
    {
        const string INPUT_FILE_NAME_PATTERN = "input-file-name-pattern";
        const string CSV_INPUT_PATH = "csv-input-path";

        protected string SourceFileNamePattern { get; private set; }
        protected string CsvSourceFolderPath { get; private set; }
        public MyCsvHandler(ILoggingProvider logger) : base(logger) { }
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

        public override Task InputProducingTask(IDataContainerStore inputStorage, CancellationToken cancellationToken)
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

                    container.MetaData = metaData;

                    inputStorage.Receive(container);
                }
            }
            return Task.CompletedTask;
        }
    }
}
