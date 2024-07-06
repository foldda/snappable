using System;
using System.IO;
using System.Threading.Tasks;
using Foldda.Automation.Framework;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;
using Charian;
using System.Collections.Generic;

namespace Foldda.Automation.HL7Handler
{
    public class HL7FileWriter : BaseHL7Handler
    {
        public enum Mode : int { ByDay, ByHour, ByRecord, BySource };

        public static readonly string EXPORT_MODE = "export-mode";
        public static readonly string PER_DAY = "PER_DAY";
        public static readonly string PER_HOUR = "PER_HOUR";
        public static readonly string PER_RECORD = "PER_RECORD";

        const string TARGET_PATH = "target-path";
        const string OUTPUT_PREFIX = "output-prefix";
        const string MLLP_SEPARATOR_ENCODE = "mllp-separator-encode";

        protected Mode _mode { get; private set; }
        protected string _targetPath { get; private set; }
        protected string _outputPrefix { get; private set; }
        protected bool _mllpSeparatorEncode { get; private set; }

        public HL7FileWriter(ILoggingProvider logger, DirectoryInfo homePath) : base(logger, homePath) { }

        public override void SetParameters(IConfigProvider config)
        {
            _targetPath = config.GetSettingValue(TARGET_PATH, string.Empty);
            if (string.IsNullOrEmpty(_targetPath))
            {
                throw new Exception($"Mandatory parameter '{TARGET_PATH}' not supplied in config.");
            }

            string fullPath = Path.GetFullPath(_targetPath);   //check if path-string is valid, or throw exception
            if (Directory.Exists(fullPath) == false)
            {
                Directory.CreateDirectory(fullPath);
                Log($"WARNING - Supplied path '{_targetPath}' does not pre-exist, and it's now created.");
            }

            _outputPrefix = config.GetSettingValue(OUTPUT_PREFIX, string.Empty);

            _mllpSeparatorEncode = config.GetSettingValue(MLLP_SEPARATOR_ENCODE, "Y", false);

            string mode = config.GetSettingValue(EXPORT_MODE, string.Empty);
            _mode = PER_DAY.Equals(mode) ? Mode.ByDay :
                (PER_HOUR.Equals(mode) ? Mode.ByHour :
                    (PER_RECORD.Equals(mode) ? Mode.ByRecord :  //per containe record
                        Mode.BySource //default - per container-source
                    )
                );
        }

        public string TypeExt { get { return ".hl7"; } } //eg, .hl7, .txt

        const string PER_HOUR_PATTERN = "yyMMdd-HH";
        const string PER_DAY_PATTERN = "yyMMdd";

        int recordCount = 0;

        //create a unique file prefix with container-id (if requred) + unique index
        Dictionary<string, int> SourceTracker = new Dictionary<string, int>();
        string GetNextFilePrefix(string containerId)
        {
            if(SourceTracker.TryGetValue(containerId, out int index))
            {
                SourceTracker.Remove(containerId);
                index++;
                SourceTracker.Add(containerId, index);
                return containerId + "-" + index;
            }
            else
            {
                SourceTracker.Add(containerId, 0);
                return containerId;
            }
        }

        public override Task OutputConsumingTask(IDataReceiver outputStorage, CancellationToken cancellationToken)
        {
            var outputReceiced = outputStorage.CollectReceived();
            if(outputReceiced.Count > 0)
            {
                int recordsWritten = 0;
                foreach(var container in outputReceiced)
                {
                    recordCount = 0;
                    string sourceContainerId = GetNextFilePrefix(container.MetaData.ToRda().ScalarValue);
                    //https://stackoverflow.com/questions/6053541/regex-every-non-alphanumeric-character-except-white-space-or-colon/6053606
                    sourceContainerId = Regex.Replace(sourceContainerId /*OriginSourceName*/, @"[^a-zA-Z\d\.]", "_");
                    foreach (HL7Message hl7 in container.Records)
                    {
                        //HL7Message hl7 = new HL7Message(hl7Record);
                        ProcessHL7Record(hl7, sourceContainerId);
                        recordsWritten++;
                    }
                }
                Log($"{outputReceiced.Count} container(s) with total {recordsWritten} records processed.");
            }

            return Task.Delay(100, cancellationToken);
        }

        protected IRda ProcessHL7Record(HL7Message record, string sourceContainerId)
         {

            string fileName;
            switch (_mode)
            {
                case Mode.ByDay:
                    {
                        fileName = $@"{_outputPrefix}{ DateTime.Now.ToString(PER_DAY_PATTERN)}";
                        break;
                    }
                case Mode.ByHour:
                    {
                        fileName = $@"{_outputPrefix}{ DateTime.Now.ToString(PER_HOUR_PATTERN)}";
                        break;
                    }
                case Mode.ByRecord:
                    {

                        recordCount++;
                        fileName = $@"{sourceContainerId}-{recordCount:D3}";
                        break;
                    }
                default:
                    {
                        /* Default is by-source */
                        fileName = sourceContainerId;
                        break;
                    }
            }

            //saving the data
            //var data = hl7.ToChars();
            //Log($"Writing {data.Length} to file '{fileName}' ...");
            try
            {
                string filePath = $@"{_targetPath}\{fileName}{TypeExt}";
                bool fileExists = File.Exists(filePath);

                using (var writer = new StreamWriter(
                                        filePath,
                                        fileExists,     /*create if file not exists, append otherwise*/
                                        Encoding.Default))
                {
                    if (_mllpSeparatorEncode)
                    {
                        //wrap message data with HL7 MLLP bytes, is this necessary?
                        writer.Write('\v'); //0x0b
                        writer.Write(record.ToChars());
                        writer.Write((char)0x1c);
                        writer.Write('\r'); //0x0d
                    }
                    else
                    {
                        if (fileExists)
                        {
                            //add a new line to separate this record to previous record
                            writer.Write('\r');
                            writer.Write('\n');
                        }
                        writer.Write(record.ToChars());
                    }
                    writer.Flush();
                }
                Log($"Written file {fileName}{TypeExt}.");
            }
            catch (Exception e)
            {
                Log($"Error saving file {fileName}{TypeExt} - {e.Message}");
                Log(e);
            }

            return record;
        }

    }

}