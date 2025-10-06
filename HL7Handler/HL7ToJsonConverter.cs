using System.Collections.Generic;
using Foldda.Automation.Framework;
using System.Threading;
using Foldda.Automation.CsvHandler;
using System.IO;
using System.Threading.Tasks;
using System;
using Charian;
using System.Text;
using static Foldda.Automation.HL7Handler.HL7DataElement;
using System.Linq;

namespace Foldda.Automation.HL7Handler
{
    /**
     * "hl7-to-json-mapping" defines which HL7 elements are of-interest, and to which Json property they shall be assigned. 
     * Eg. if in the config there is hl7-to-json-mapping="PID-5-1|Last Name" and in the HL7 message PID-5-1 has the value "Chen", the encoded Json would be - 
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
    public class HL7ToJsonConverter : BaseHL7Handler
    {
        public const string HL7_TO_JSON_MAPPING = "hl7-to-json-mapping";

        public HL7ToJsonConverter(IHandlerManager manager) : base(manager) { }
        Dictionary<string, SegmentDataElementSelector> elementSelectors = new Dictionary<string, SegmentDataElementSelector>(); 

        public override void Setup(IConfigProvider config)
        {
            elementSelectors.Clear();
            List<string> mappings = config.GetSettingValues(HL7_TO_JSON_MAPPING);

            /*** Eg.
             * 
              <Parameters>
                <Parameter>
                  <Name>hl7-to-json-mapping</Name>
                  <Value>Last name|PID-5.1</Value>
                </Parameter>  
                <Parameter>
                  <Name>hl7-to-json-mapping</Name>
                  <Value>First name|PID-5.2</Value>
                </Parameter>  
                <Parameter>
                  <Name>hl7-to-json-mapping</Name>
                  <Value>DOB|PID-7</Value>
                </Parameter>  
                <Parameter>
                  <Name>hl7-to-json-mapping</Name>
                  <Value>MRN|PID-3</Value>
                </Parameter>  
                <Parameter>
                  <Name>hl7-to-json-mapping</Name>
                  <Value>Message UID|MSH-10</Value>
                </Parameter>  
              </Parameters>

             */
            foreach (string s in mappings)
            {
                int index = s.IndexOf('|');
                elementSelectors.Add(s.Substring(0, index), new SegmentDataElementSelector(s.Substring(index + 1)));
            }
        }

        protected override Task ProcessInputHL7MessageRecord(HL7Message hl7, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            StringBuilder jsonStringBuilder = new StringBuilder("{");
            foreach (var jsonProp in elementSelectors.Keys)
            {
                jsonStringBuilder.Append(MakeJsonProperty(hl7, jsonProp, elementSelectors.TryGetValue(jsonProp, out SegmentDataElementSelector selector) ? selector : null)).Append(',');
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
                Log($"WARNING: HL7 message [{hl7}] has no matching segment elements and is skipped.");
            }

            return Task.Delay(50);
        }

        //give a selector, return the elements' value of the targeted segment as a named Json property
        private string MakeJsonProperty(HL7Message hl7, string jsonPropName, SegmentDataElementSelector selector)
        {
            StringBuilder jsonProbSubStringBuilder = new StringBuilder();

            foreach(var segment in hl7.GetSegments(selector.SegmentName))
            {
                jsonProbSubStringBuilder.Append($"\n\"{jsonPropName}\":\"");    //JSON prop opening

                //note - the selector fills the path's excluded-element collection
                SegmentDataElementSelector.SelectorResult selection = selector.SelectFrom(segment, null);
                var elements = selection.GetQualifiedDataElements(null);
                jsonProbSubStringBuilder.Append(string.Join(",", elements.Select(o=>o.Value)));    //qulified elelemnts in the segment

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