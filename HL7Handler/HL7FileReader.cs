using Charian;
using Foldda.DataAutomation.Framework;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.HL7Handler
{
    public class HL7FileReader : BaseHL7Handler
    {
        public HL7FileReader(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        const string FILE_NAME_PATTERN = "file-name-pattern";
        const string SOURCE_PATH = "source-path";

        protected string TargetFileNamePattern { get; private set; }
        protected string SourcePath { get; private set; }

        public override void SetParameters(IConfigProvider config)
        {
            var paramSourcePath = config.GetSettingValue(SOURCE_PATH, string.Empty);
            if (string.IsNullOrEmpty(paramSourcePath))
            {
                throw new Exception($"ERROR - parameter '{SOURCE_PATH}' is mandatory and it's not supplied.");
            }
            else if (!Directory.Exists(paramSourcePath))
            {
                throw new Exception($"ERROR - supplied path '{paramSourcePath}' does not exist.");
            }
            else
            {
                SourcePath = paramSourcePath;
            }

            var paramFileName = config.GetSettingValue(FILE_NAME_PATTERN, string.Empty);
            if (string.IsNullOrEmpty(paramFileName))
            {
                throw new Exception($"ERROR - parameter '{FILE_NAME_PATTERN}' is mandatory and it's not supplied.");
            }
            
            //parameters checked OK
            TargetFileNamePattern = paramFileName;        
        }

        public override Task InputProducingTask(IDataReceiver inputStorage, CancellationToken cancellationToken)
        {
            DirectoryInfo targetDirectory = new DirectoryInfo(SourcePath);

            var result = ScanDirectory(targetDirectory, TargetFileNamePattern, SkippedFileList, GetDefaultFileRecordScanner(Logger), Logger, cancellationToken).Result;
            foreach (var container in result)
            {
                inputStorage.Receive(container);
            }
            return Task.CompletedTask;
        }

    }
}
