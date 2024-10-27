using Charian;
using Foldda.Automation.Framework;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.HL7Handler
{
    public class HL7FileReader : BaseHL7Handler
    {
        public HL7FileReader(ILoggingProvider logger) : base(logger) { }

        const string FILE_NAME_PATTERN = "file-name-pattern";
        const string SOURCE_PATH = "source-path";

        //protected string TargetFileNamePattern { get; private set; }
        //protected string SourcePath { get; private set; }

        protected FileReaderConfig DefaultFileReaderConfig { get; private set; }

        public override void SetParameter(IConfigProvider config)
        {
            string SourcePath = config.GetSettingValue(SOURCE_PATH, string.Empty);
            if (string.IsNullOrEmpty(SourcePath)|| !Directory.Exists(SourcePath))
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

        protected override async Task ProcessHandlerEvent(HandlerEvent handlerEvent, CancellationToken cancellationToken)
        {
            //If the input handler-event does not contain file-reading config instructions, 
            //use the settings from the local config file.
            if (!(handlerEvent.EventDetailsRda is FileReaderConfig readConfig))
            {
                Log($"Input event has no file-reading instruction, local config settings are used.");
                readConfig = DefaultFileReaderConfig;
            }

            await ScanHL7Data(readConfig, cancellationToken);
        }

        private Task ScanHL7Data(FileReaderConfig readConfig, CancellationToken cancellationToken)
        {
            try
            {
                DirectoryInfo targetDirectory = new DirectoryInfo(readConfig.InputFilePath);

                var result = ScanDirectory(targetDirectory, readConfig.InputFileNameOrPattern, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;
                foreach (var container in result)
                {
                    OutputStorage.Receive(container);
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
