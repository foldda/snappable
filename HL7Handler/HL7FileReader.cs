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

        protected string TargetFileNamePattern { get; private set; }
        protected string SourcePath { get; private set; }

        public override void SetParameters(IConfigProvider config)
        {
            SourcePath = config.GetSettingValue(SOURCE_PATH, string.Empty);
            if (string.IsNullOrEmpty(SourcePath)|| !Directory.Exists(SourcePath))
            {
                Log($"ERROR - supplied path '{SourcePath}' does not exist.");
            }

            var paramFileName = config.GetSettingValue(FILE_NAME_PATTERN, string.Empty);
            if (string.IsNullOrEmpty(paramFileName))
            {
                Log($"ERROR - parameter '{FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
            }
            
            //parameters checked OK
            TargetFileNamePattern = paramFileName;        
        }

        public override Task InputProducingTask(IDataContainerStore inputStorage, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(SourcePath) || !Directory.Exists(SourcePath))
                {
                    throw new Exception($"ERROR - supplied path '{SourcePath}' does not exist.");
                }
                DirectoryInfo targetDirectory = new DirectoryInfo(SourcePath);

                var result = ScanDirectory(targetDirectory, TargetFileNamePattern, SkippedFileList, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;
                foreach (var container in result)
                {
                    inputStorage.Receive(container);
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
