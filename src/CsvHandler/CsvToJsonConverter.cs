using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using System.Threading.Tasks;
using System;
using Charian;
using System.Text;
using System.Linq;
using static Foldda.Automation.CsvHandler.CsvFileWriter;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;

namespace Foldda.Automation.CsvHandler
{
    /**
     * "csv-column-to-json-mapping" defines which HL7 elements are of-interest, and to which Json property they shall be assigned. 
     * Eg. if in the config there is csv-column-to-json-mapping="PID-5-1|Last Name" and in the HL7 message PID-5-1 has the value "Chen", the encoded Json would be - 
     * 
     * {
     *      ...
          "Last Name":"Chen",
            ...
       }
     * 
     * If there are multiple PID segments, and in the second PID it has the PID-5-1 value as "Smith", then - 
     * {
     *      ...
          "Last Name":"Chen",
          "Last Name":"Smith",
            ...
       }
     */
    public class CsvToJsonConverter : BaseCsvHandler
    {
        public const string CSV_COLUMN_TO_JSON_MAPPING = "csv-column-to-json-mapping";

        public CsvToJsonConverter(ISnappableManager manager) : base(manager) { }

        private Dictionary<string, string> _csvMappings = new Dictionary<string, string>();

        public override void Setup(IConfigProvider config)
        {
            _csvMappings.Clear();
            List<string> mappings = config.GetSettingValues(CSV_COLUMN_TO_JSON_MAPPING);

            /*** Eg.<Value>Json element name|Csv column name</Value>, 
             * if mapping isn't available, the Csv column header is default to be the output Json element name
             * 
              <Parameters>
                <Parameter>
                  <Name>csv-column-to-json-mapping</Name>
                  <Value>Json element name|Csv column name</Value>
                </Parameter>  

                ...

                <Parameter>
                  <Name>csv-column-to-json-mapping</Name>
                  <Value>eg. Message UID|Column N header</Value>
                </Parameter>  
              </Parameters>

             */
            foreach (string s in mappings)
            {
                int index = s.IndexOf('|');
                _csvMappings.Add(s.Substring(0, index), s.Substring(index + 1));
            }
        }

        protected override Task ProcessTabularRecord(TabularRecord csvRecord, RecordContainer outputContainer, CancellationToken cancellationToken)
        {            
            //Csv container label would carry the meta-data such as the source-file-id, also columns name, data-types etc.
            TabularRecord.MetaData csvContainerMetaData = TabularRecord.GetMetaData(outputContainer.MetaData.ToRda());
            TabularRecord csvHeader = new TabularRecord(csvContainerMetaData.ColumnNames);

            StringBuilder jsonStringBuilder = new StringBuilder("{");
            jsonStringBuilder.Append(MakeJsonProperty(csvHeader, csvRecord).Append(','));
            foreach (var jsonProp in _csvMappings.Keys)
            {
            }

            if (jsonStringBuilder.Length > 1)
            {
                //remove the last appended comma
                jsonStringBuilder.Remove(jsonStringBuilder.Length - 1, 1);
                jsonStringBuilder.Append("\n}");
                Rda rda = new Rda() { ScalarValue = jsonStringBuilder.ToString() };
                outputContainer.Add(rda);
            }
            else
            {
                Log($"WARNING: CSV record [{csvRecord}] has no matching elements and is skipped.");
            }

            return Task.CompletedTask;
        }

        //give a selector, return the elements' value of the targeted segment as a named Json property
        private string MakeJsonProperty(TabularRecord csvHeader, TabularRecord csvRow)
        {
            StringBuilder jsonProbSubStringBuilder = new StringBuilder();

            //foreach (var headerColumn in csvHeader.ItemValues)
            for(int i = 0; i < csvHeader.ItemValues.Count; i++)
            {
                var headerColumn = csvHeader.ItemValues[i];
                string jsonPropName = _csvMappings.TryGetValue(headerColumn, out string mapped) ? mapped : headerColumn;
                jsonProbSubStringBuilder.Append($"\n\"{jsonPropName}\":\"{csvRow.ItemValues[i]}\"");    //JSON prop opening

                jsonProbSubStringBuilder.Append($"\",");    //JSON prop closing
            }

            //remove the last comma
            if (jsonProbSubStringBuilder.Length > 0)
            {
                jsonProbSubStringBuilder.Remove(jsonProbSubStringBuilder.Length - 1, 1);
            }

            return jsonProbSubStringBuilder.ToString();
        }
    }
}