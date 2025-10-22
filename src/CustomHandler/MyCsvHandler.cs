using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foldda.Automation.CsvHandler;
using Foldda.Automation.Framework;

namespace Foldda.Custom.Handler
{
    /// <summary>
    /// Class MyCompany.MyApp.MyCustomHandler.MyCsvFileReader illustrates how you can make your own custom Foldda runtime-compatible hanlder.
    /// 
    /// As the starting point, apart from the name change, MyCsvFileReader is an exact copy of the original 'official' CsvFileReader handler, so if compiled as it is, 
    /// this handler does the same function as the CsvFileReader, except it resides in a different name space and DLL which is separate from the official Foldda 
    /// distribution, which is under your control. 
    /// 
    /// In other words, from here you can make change to this handler in this Dll to have any custom feature you want, without interferring with the other Foldda handlers.
    /// 
    /// For example, you can make this handler to read input multiple files simutanously from several locations, or to do something entirely irrelavent to file-reading.
    /// </summary>
    public class MyCsvFileReader : BaseCsvHandler
    {
        public const string INPUT_FILE_NAME_PATTERN = "input-file-name-pattern";
        public const string INPUT_FILE_PATH = "csv-input-path";

        internal FileReaderConfig LocalConfig { get; private set; }
        public MyCsvFileReader(ILoggingProvider logger) : base(logger) { }
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
                    Log($"Container has no file-download instrcution, local (FTP) config settings are used.");
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
