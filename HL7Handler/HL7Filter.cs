using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Charian;
using Foldda.Automation.Framework;
using static Foldda.Automation.HL7Handler.HL7DataElement;

namespace Foldda.Automation.HL7Handler
{
    public class HL7Filter : BaseHL7Handler
    {
        public const string FILTERING_RULE = "filtering-rule";
        protected List<SelectionPathDefilition> MatchingRules { get; private set; } = new List<SelectionPathDefilition>();

        public HL7Filter(IHandlerManager manager) : base(manager) { }

        public override void Setup(IConfigProvider config)
        {
            MatchingRules.Clear(); 
            var parameters = config.GetSettingValues(FILTERING_RULE); 
            if (parameters != null)
            {
                foreach (var rule in parameters)
                {
                    MatchingRules.Add(new SelectionPathDefilition(rule));
                }
            }            
        }

        protected override Task ProcessInputHL7MessageRecord(HL7Message record, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            if(FilterHL7Message(record))
            {
                outputContainer.Add(record);
            }
            return Task.Delay(50);
        }

        private bool FilterHL7Message(HL7Message hl7)
        {
            string msh10 = hl7.MSH.Fields[9]?.Value ?? string.Empty;
            string msh9 = hl7.MSH.Fields[8]?.Value ?? string.Empty;

            foreach (SelectionPathDefilition rule in MatchingRules)
            {
                int matchedCount = rule.GetQualifiedPaths(hl7.Segments)?.Count ?? 0;
                if (matchedCount > 0)
                {
                    Log($"Message (MSH-9={msh9}, MSH-10={msh10}) passed rule [{rule}].");

                    return true;   //passes if one of the rules matched.
                }
            }

            Log($"Message (MSH-9={msh9}, MSH-10={msh10}) failed all rules and is filtered.");
            return false;
        }

        /* 
         * A 'path' is a list of segments (add their associated selections) that qualifies the given segment-group definition.
         * Used for holding valid elements from the given HL7 record. then apply each selector ("Filtering") to segments in the path
         * Disqualified (non-matching) elements are placed in the path's exclusion list.
         * Then a 'qualified-path' serves as 1) 'for retriving final selected values', and 2) as a "context" for further selections.
         */
        public class QualifiedSelectionPath
        {
            //SelectionPathDefilition PathDefilition { get; }

            //one for each selector, in order. Each result 
            public List<SegmentDataElementSelector.SelectorResult> PathSelectorsResults { get; } = new List<SegmentDataElementSelector.SelectorResult>();
            public Dictionary<string, HL7Segment> SegmentGroup { get; }  //all "related" segments including the target

            //these are disqualified data-elements (and associated repeats) that will be excluded for further
            //selector qualification.
            public List<HL7DataElement> ExcludedDataElements { get; } = new List<HL7DataElement>();

            private QualifiedSelectionPath() { }

            public QualifiedSelectionPath(Dictionary<string, HL7Segment> grouppedSegment)
            {
                this.SegmentGroup = grouppedSegment;
                //PathDefilition = selectionPathDefilition;
            }

            //last selector's elements - for assign new value to....
            public List<DataElementLocation> GetPathEndDataElements()
            {
                if (PathSelectorsResults.Count > 0)
                {
                    List<DataElementLocation> result = new List<DataElementLocation>();
                    foreach (var element in PathSelectorsResults.Last().GetQualifiedDataElements(ExcludedDataElements))
                    {
                        result.Add(new DataElementLocation() { Path = this, DataElement = element });
                    }
                    return result;
                }
                return null;
            }

            //  for producing combined selectors selected values
            public List<List<string>> GetValuesCsv()
            {
                List<List<HL7DataElement>> finalCsv = new List<List<HL7DataElement>>();
                bool isFirstSelection = true;

                //progressively iterate through each column along the seletion-path, picking up qualified values from selected data-element
                //each selection may have result of multiple selected elements
                //so they need to be added to each of the existing rows (so it's a many-to-many multiplication)
                foreach (var selectorResult in PathSelectorsResults /* columns */)
                {
                    if (isFirstSelection)
                    {
                        foreach (var element in selectorResult.GetQualifiedDataElements(ExcludedDataElements))
                        {
                            finalCsv.Add(new List<HL7DataElement>() { element });
                        }

                        isFirstSelection = false;
                    }
                    else
                    {
                        var currentCsvBlock = finalCsv;
                        finalCsv = new List<List<HL7DataElement>>();   //clear

                        //[multiplication] add each of the selected element ...
                        foreach (var currentRow in new List<List<HL7DataElement>>(currentCsvBlock))
                        {
                            //for each 'currentRow', the appending new column value can be different
                            List<HL7DataElement> newColumnElements = selectorResult.GetQualifiedDataElements(ExcludedDataElements, currentRow);
                            foreach (var element in newColumnElements)
                            {
                                List<HL7DataElement> newRow = new List<HL7DataElement>(currentRow); //clone
                                newRow.Add(element);  //add new column with element value
                                finalCsv.Add(newRow);
                            }
                        }
                    }
                }

                //now convert finalCsv from elemens to 'string values'
                List<List<string>> result = new List<List<string>>();
                foreach (var row in finalCsv)
                {
                    List<string> strRow = new List<string>();
                    foreach (var col in row)
                    {
                        strRow.Add(col.Value);
                    }
                    result.Add(strRow);
                }

                return result;
            }

            //return TRUE if the path's Segment satisfies all the selectors, and 
            internal bool ApplySelectors(List<SegmentDataElementSelector> selectors)
            {
                //List<SelectorResult> qualifiedSelection = new List<SelectorResult>();
                foreach (var selector in selectors)
                {
                    //adds a "selection for the selector" also sets exclusion context as each selector filter-thru
                    if (SegmentGroup.TryGetValue(selector.SegmentName, out HL7Segment segment))
                    {
                        //note - the selector fills the path's excluded-element collection
                        SegmentDataElementSelector.SelectorResult selection = selector.SelectFrom(segment, ExcludedDataElements/*gradualy built by each selector*/);

                        PathSelectorsResults.Add(selection);    //selection can be empty
                    }
                }

                foreach (var selection in PathSelectorsResults)
                {
                    //if any selector's result is empty, return FALSE
                    if (selection.GetQualifiedDataElements(ExcludedDataElements/*this is complete/static list */).Count == 0)
                    {
                        return false;
                    }
                }

                return true;    //all selectors' retult has value (but value may be virtual);
            }

            //creates a selector-result object to the filter-result collection
            //also fills ExcludedDataElements which will be applicable to the following selectors
            //sets context and maybe the IsQualified flag


            //check the current path contain segments from the filtering string
            public bool Covers(string filteringString)
            {
                foreach (var s in filteringString.Split('~'))
                {
                    if (!SegmentGroup.ContainsKey(s.Trim().Substring(0, 3)))
                    {
                        return false;
                    }
                }

                return true;
            }

            // allows a caller to furher refine the selection from the qualified elements
            // by providing a filter string, this method construct selectors out of the filter-string, 
            // then filter the current selection.
            // this method is used by Foldda Function, where the (left of func) target is the processing context
            // and the function (right) uses this method to select source input data in the context.
            public List<HL7DataElement> RefineSelection(string dataElementFilters)
            {
                SegmentDataElementSelector.SelectorResult selectorResult = null;
                List<HL7DataElement> excludedDataElements = new List<HL7DataElement>();   //"local"

                foreach (var s in dataElementFilters.Split('~'))
                {
                    var selector = new SegmentDataElementSelector(s);
                    if (SegmentGroup.TryGetValue(selector.SegmentName, out HL7Segment segment))
                    {
                        selectorResult = selector.SelectFrom(segment, excludedDataElements);
                    }
                }

                return selectorResult?.GetQualifiedDataElements(new List<HL7DataElement>() /* a blank exclusion context */, excludedDataElements);
            }

            //a slected data-element with its "selection context", 
            //used for referencing an element '$' in a HL7 record
            public class DataElementLocation
            {
                public QualifiedSelectionPath Path { get; set; }    //the path leads to this location, eg, "selection context"
                public HL7DataElement DataElement { get; set; }
            }


        }

        /* 
 * a pathway for locating data-elements by using a list of segment-selectors to match aganist a list of provided segments
 * a segment-data.element-selector is a string consists of a segment-name and a in-segment data-element address (indexes , and optionally a value-filter)
 */
        public class SelectionPathDefilition
        {
            public List<SegmentDataElementSelector> Selectors { get; } = new List<SegmentDataElementSelector>();

            List<string> UniqueSegmentNameList { get; } = new List<string>();
            SegmentDataElementSelector TargetDataElementSelector => Selectors.Count > 0 ? Selectors.Last() : null;

            public SelectionPathDefilition(string path)
            {
                string[] _selectorStrings = path.Split('~');
                HashSet<string> uniqueNames = new HashSet<string>();
                //construct selectors into a ordered list
                foreach (var s in _selectorStrings)
                {
                    Selectors.Add(new SegmentDataElementSelector(s));
                    string segName = s.Substring(0, 3);
                    if (uniqueNames.Add(segName))
                    {
                        UniqueSegmentNameList.Add(segName);
                    }
                }
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                foreach (var s in GetSelectorExpressionStrings())
                {
                    sb.Append(s).Append('~');
                }
                if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);
                return sb.ToString();
            }

            /* A 'path' is a list of segments (add their associated selections) that qualifies the given segment-group definition.
             * This method retrives all the valid paths from the given segments, then apply each selector ("Filtering") to segments in the path
             * Disqualified (non-matching) elements are placed in the path's exclusion list.
             * Then a 'qualified-path' serves as 1) 'for retriving final selected values', and 2) as a "context" for further selections.
             */
            public List<QualifiedSelectionPath> GetQualifiedPaths(List<HL7Segment> candidateSegments)
            {
                List<QualifiedSelectionPath> result = new List<QualifiedSelectionPath>();

                //first pass is to get segments combination that are in the "required order" (by names only) of this location path
                List<Dictionary<string, HL7Segment>> nameMatchedSegmentGroups =
                    GetNameMatchingSegmentGroups(candidateSegments, 0, candidateSegments.Count, UniqueSegmentNameList, 0); //recurrsion

                //second pass is to apply all the selectors to each segment-group
                foreach (var nameMatchedSegmentGroup in nameMatchedSegmentGroups)
                {
                    QualifiedSelectionPath candidate = new QualifiedSelectionPath(nameMatchedSegmentGroup);

                    if (candidate.ApplySelectors(Selectors) == true)
                    {
                        result.Add(candidate);  //the selection-path (candidate) is filled with selector-results
                    }
                }

                //result wil be empty unless one or more segment-group's data-elements are qualified
                return result;
            }

            public List<string> GetSelectorExpressionStrings()
            {
                List<string> headerRow = new List<string>();
                foreach (var selector in this.Selectors)
                {
                    headerRow.Add(selector.ToString());
                }
                return headerRow;
            }

            //check in the given range, for the segment-name-to-match
            //if match is found, recursive to match the next segment, until full match is found
            //else failed
            private List<Dictionary<string, HL7Segment>> GetNameMatchingSegmentGroups
                (
                    List<HL7Segment> allSegments,
                    int rangeStartIndexInclusive,
                    int rangeEndIndexExclusive,
                    List<string> segmentGroupNamesSequence,
                    int segmentNameToMatchIndex
                )
            {
                if (rangeStartIndexInclusive >= rangeEndIndexExclusive) { return null; }

                //if segment name to match is at the tail of the sequence
                //find every matched segments in the range, and return the result
                //if it's a tail-match (i.e. full-match), 
                string segmentNameToMatch = segmentGroupNamesSequence[segmentNameToMatchIndex];
                if (segmentNameToMatchIndex == segmentGroupNamesSequence.Count - 1)
                {
                    var resultAtThisLevel = new List<Dictionary<string, HL7Segment>>();
                    for (int i = rangeStartIndexInclusive; i < rangeEndIndexExclusive; i++)
                    {
                        var currentSegment = allSegments[i];
                        //return tail-matched result (no further recurrsion)
                        if (currentSegment.Name.Equals(segmentNameToMatch))
                        {
                            resultAtThisLevel.Add(new Dictionary<string, HL7Segment>()
                            {
                                { currentSegment.Name, currentSegment }
                            });
                        }
                    }
                    return resultAtThisLevel;
                }
                else
                {
                    var resultAtThisLevel = new List<Dictionary<string, HL7Segment>>();
                    int headerSegmentIndexForSubRange = -1;
                    for (int i = rangeStartIndexInclusive; i < rangeEndIndexExclusive; i++)
                    {
                        var currentSegment = allSegments[i];
                        //return tail-matched result (no further recurrsion)
                        if (currentSegment.Name.Equals(segmentNameToMatch))
                        {
                            //create a sub-range for recurrsion
                            if (headerSegmentIndexForSubRange >= 0)
                            {
                                MergeHeadSegmentWithSubRangeRecurrsionResults
                                (
                                    resultAtThisLevel,
                                    headerSegmentIndexForSubRange,
                                    allSegments,
                                    //headerSegmentIndexForSubRange + 1,   //new sub-Range Start Index Inclusive,
                                    i,  //newRangeEndIndexExclusive,
                                    segmentGroupNamesSequence,
                                    segmentNameToMatchIndex + 1
                                );
                            }
                            //mark the "last matched" position for next sub-range
                            headerSegmentIndexForSubRange = i;
                        }
                    }

                    //the last sub-range is ended by the end-of-all-segments, again, do recurrsion to it
                    if (headerSegmentIndexForSubRange >= 0)
                    {
                        MergeHeadSegmentWithSubRangeRecurrsionResults
                        (
                            resultAtThisLevel,
                            headerSegmentIndexForSubRange,
                            allSegments,
                            rangeEndIndexExclusive,
                            segmentGroupNamesSequence,
                            segmentNameToMatchIndex + 1
                        );
                    }

                    return resultAtThisLevel;
                }
            }

            private void MergeHeadSegmentWithSubRangeRecurrsionResults
            (
                List<Dictionary<string, HL7Segment>> resultAtThisLevel,
                int headerSegmentIndexForSubRange,
                List<HL7Segment> allSegments,
                int subRangeEndIndexExclusive,
                List<string> segmentGroupNamesSequence,
                int segmentNameToMatchIndex
            )
            {
                //recurrsion - get result in "sub-range" form child-segments
                var anyFullMatchFromChildLevel = GetNameMatchingSegmentGroups
                                                (
                                                    allSegments,
                                                    headerSegmentIndexForSubRange + 1,  //sub-range-start: from below header-segment
                                                    subRangeEndIndexExclusive,
                                                    segmentGroupNamesSequence,
                                                    segmentNameToMatchIndex
                                                );

                //merge this sub-range's result, if any
                if (anyFullMatchFromChildLevel != null)
                {
                    HL7Segment headSegment = allSegments[headerSegmentIndexForSubRange];
                    //merge current-match into children recursion returned results ...
                    foreach (var resultAtChildLevel in anyFullMatchFromChildLevel)
                    {
                        //add this segment to the returned child-result
                        resultAtChildLevel.Add(headSegment.Name, headSegment);
                        //this is a result of a full-qualified segment-list (at this level)
                        resultAtThisLevel.Add(resultAtChildLevel);
                    }
                }
            }
        }
    }
}