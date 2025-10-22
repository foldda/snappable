using Charian;
using Foldda.Automation.Framework;
using Foldda.Automation.HL7Handler;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Custom.Handler
{
    /// <summary>
    /// Class MyCompany.MyApp.MyCustomHandler.MyHL7Handler illustrates how you can make your own custom Foldda runtime compatible hanlder.
    /// 
    /// As the starting point, MyHL7Handler is an exact copy of the HL7FileReader handler, so if compiled as it is, it will be your "own (file-reader) handler" that does 
    /// the same function, except it will reside in a different DLL which is separate from the official Foldda distribution and will be under your control.
    /// In other words, from here you can change this handler in this Dll to have any custom feature you want, without interferring with the other Foldda handlers.
    /// 
    /// For example, you can make this handler to read input multiple files simutanously from several locations, or to do something entirely irrelavent to file-reading.
    /// </summary>
    public class MyHL7FileReader : BaseHL7Handler
    {
        public MyHL7FileReader(ISnappableManager manager) : base(manager) { }

        const string FILE_NAME_PATTERN = "file-name-pattern";
        const string SOURCE_PATH = "source-path";

        protected FileReaderConfig DefaultFileReaderConfig { get; private set; }

        public override void Setup(IConfigProvider config)
        {
            string SourcePath = config.GetSettingValue(SOURCE_PATH, string.Empty);
            if (string.IsNullOrEmpty(SourcePath) || !Directory.Exists(SourcePath))
            {
                Log($"ERROR - supplied path '{SourcePath}' does not exist.");
            }

            var paramFileNamePattern = config.GetSettingValue(FILE_NAME_PATTERN, string.Empty);
            if (string.IsNullOrEmpty(paramFileNamePattern))
            {
                Log($"ERROR - parameter '{FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
            }

            string TargetFileNamePattern = paramFileNamePattern;

            //parameters checked OK
            DefaultFileReaderConfig = new FileReaderConfig()
            {
                InputFilePath = SourcePath,
                InputFileNameOrPattern = TargetFileNamePattern
            };
        }

        protected override async Task ProcessHandlerEvent(MessageRda.HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            //ATM, any event would trigger a read action.
            if (!(handlerEvent.EventDetailsRda is FileReaderConfig readConfig))
            {
                readConfig = DefaultFileReaderConfig;
            }

            await ScanHL7Data(readConfig, cancellationToken);
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
