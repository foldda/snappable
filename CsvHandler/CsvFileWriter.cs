using System;
using System.IO;
using System.Threading.Tasks;
using Foldda.Automation.Framework;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;
using Charian;
using System.Collections.Generic;
using System.Linq;

namespace Foldda.Automation.CsvHandler
{
    public class CsvFileWriter : BaseCsvHandler
    {
        public enum Mode : int { ByDay, ByHour, ByRecord, BySource };

        const string OUTPUT_FOLDER_PATH = "csv-output-path";
        const string EXPORT_MODE = "export-mode";

        //EXPORT_MODE options
        public static readonly string PER_DAY = "PER_DAY";
        public static readonly string PER_HOUR = "PER_HOUR";
        public static readonly string PER_RECORD = "PER_RECORD";
        public static readonly string PER_SOURCE_ID = "PER_SOURCE_ID";

        //prefix a tab-char to a cell will prevent Excel to (automatically and undesirably) render this cell as certain data-type
        //const string COLUMNS_TO_BE_PREFIXED = "prevent-excel-auto-format";   //eg "$1,$3,$4"

        protected HashSet<int> ColumnsToPrefixWithTabChar { get; private set; } = new HashSet<int>();

        protected string OutputFolderPath { get; private set; }

        protected Mode _mode { get; private set; }

        public CsvFileWriter(ILoggingProvider logger) : base(logger) { }

        public override void SetParameters(IConfigProvider config)
        {
            base.SetParameters(config); //constructs the RecordEncoding

            OutputFolderPath = config.GetSettingValue(OUTPUT_FOLDER_PATH, string.Empty); 

            if (string.IsNullOrEmpty(OutputFolderPath))
            {
                throw new Exception($"Mandatory parameter '{OUTPUT_FOLDER_PATH}' not found in config.");
            }
            string fullPath = Path.GetFullPath(OutputFolderPath);   //check if path is valid, or throw exception
            if (Directory.Exists(fullPath) == false)
            {
                Directory.CreateDirectory(fullPath);
                Log($"WARNING - Supplied output path '{OutputFolderPath}' does not exist, it's now been created.");
            }

            string mode = config.GetSettingValue(EXPORT_MODE, PER_SOURCE_ID);
            _mode = PER_DAY.Equals(mode) ? Mode.ByDay :
                (PER_HOUR.Equals(mode) ? Mode.ByHour :
                    (PER_RECORD.Equals(mode) ? Mode.ByRecord :  //per containe record
                        Mode.BySource //default - per container-source
                    )
                );

        }

        public string TypeExt { get; } = ".csv";  //eg, .hl7, .txt

        public override Task OutputConsumingTask(IDataContainerStore outputStorage, CancellationToken cancellationToken)
        {
            var outputReceiced = outputStorage.CollectReceived();
            if (outputReceiced.Count > 0)
            {
                int recordsWritten = 0;
                foreach (var container in outputReceiced)
                {
                    ProcessCsvContainer(container, cancellationToken);
                }
                Log($"{outputReceiced.Count} container(s) with total {recordsWritten} records processed.");
            }

            return Task.Delay(100, cancellationToken);
        }

        protected void ProcessCsvContainer(DataContainer container, CancellationToken cancellationToken)
        {
            //Csv container label would carry the meta-data such as the source-file-id, also columns name, data-types etc.
            TabularRecord.MetaData csvContainerMetaData = TabularRecord.GetMetaData(container.MetaData.ToRda());

            //https://stackoverflow.com/questions/6053541/regex-every-non-alphanumeric-character-except-white-space-or-colon/6053606
            //change non aplha-numeric (except period) to under-score
            string fileName = Regex.Replace(csvContainerMetaData.SourceId, @"[^a-zA-Z\d\.]", "_");      //@"[^a-zA-Z\d\s:]";
            if (_mode == Mode.ByDay || _mode == Mode.ByHour)
            {
                DateTime now = System.DateTime.Now;
                string pattern = (_mode == Mode.ByDay ? "yyyyMMdd" : "yyyyMMdd-HH");
                fileName = $@"{now.ToString(pattern)}";
            }

            if (_mode == Mode.ByRecord)
            {
                int index = 0;
                //construct the file name per record
                string outputFileName = $@"{fileName}-{index++}";
                string filePath = $@"{OutputFolderPath}\{outputFileName}{TypeExt}";
                foreach (TabularRecord csvRow in container.Records)
                {
                    WriteRecordLineToFile(filePath, csvRow, true);  //overwrite
                }
            }
            else
            {
                string filePath = $@"{OutputFolderPath}\{fileName}{TypeExt}";

                //if the file is first created, add a header row
                if (!File.Exists(filePath))
                {
                    //TabularRecord.MetaData meta = TabularRecord.GetMetaData(container.MetaData);
                    if (csvContainerMetaData.ColumnNames?.Length > 0)
                    {
                        TabularRecord csvHeaderRow = new TabularRecord(csvContainerMetaData.ColumnNames.ToList());
                        WriteRecordLineToFile(filePath, csvHeaderRow, true);  //create a file with header line columns
                    }
                }

                foreach (TabularRecord csvDataRow in container.Records)
                {
                    WriteRecordLineToFile(filePath, csvDataRow, false);  //append
                }
            }

            Log($"Wrote {container.Records.Count} lines into file '{fileName}{TypeExt}'.");

            return;

        }

        private void WriteRecordLineToFile(string filePath, TabularRecord csvRow, bool overwriteIfExists)
        {
            try
            {
                char[] data = csvRow.ToString(RecordEncoding).ToCharArray();
                using (FileStream fileStream = new FileStream(filePath, FileMode.Append))
                {
                    if (overwriteIfExists) { fileStream.SetLength(0); }

                    using (TextWriter writer = new StreamWriter(new BufferedStream(fileStream)))
                    {
                        writer.Write(data);
                        writer.Write(Environment.NewLine);
                        writer.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                Log(e);
            }
        }
    }
}
