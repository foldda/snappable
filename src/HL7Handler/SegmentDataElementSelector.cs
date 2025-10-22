using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Foldda.Automation.HL7Handler
{
    public class SegmentDataElementSelector
    {
        readonly string EQUAL_OPERATOR = "==";
        readonly string NOT_EQUAL_OPERATOR = "!=";

        ////0-base internal, -1 meaning UNSPECIFIED
        public int[] UID { get; } = null;
        //value-matching regular expression
        public Regex Regex { get; } = null;
        //a flag for qualify/dis-qualify a matched data-element
        public bool ExlcudeMatched { get; } = false;   //not-equal match
                                                       //a flag indicating whether create and return a data-element if it doesn't currently exist
                                                       //public bool ForceCreate { get; private set; } = false;

        public string SegmentName { get; } = null;
        public string DataElementIndexString { get; internal set; }
        public string DataElementValueMatchingString { get; private set; }
        private List<HL7DataElement> ExcludedRepeats { get; } = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectorString">address with optional filter, eg PID-5.1==xyz|abc or PID-5.2!=123</param>
        public SegmentDataElementSelector(string selectorString /* eg PID-5 */)
        {
            //determine excludion-flag based on presence of !=
            ExlcudeMatched = (selectorString.IndexOf(NOT_EQUAL_OPERATOR) >= 0);

            //construct the conditional RegEx, based on splitting (!= or ==)
            string[] ruleParts = selectorString.Split(
                new string[] { EQUAL_OPERATOR, NOT_EQUAL_OPERATOR }, StringSplitOptions.None);

            SegmentName = ruleParts[0].Substring(0, 3);
            if (ruleParts[0].Length > 4 && ruleParts[0].IndexOf('-') > 2)
            {
                DataElementIndexString = ruleParts[0].Substring(ruleParts[0].IndexOf('-') + 1);
                //parse the element address/path into the internal identifying indexes int[] UID, eg
                /* field-level address: (PID-) 5  =>  UID int[]{4} //UID is 0-based index
                 * component-level address (repeat-not-specified): PID-5.1, =>  UID int[]{4,-1,0}
                 * component-level address (repeat-specified): PID-5(2).1, =>  UID int[]{4,1,0}
                 * sub-component-level address (repeat-specified): PID-5(2).1.2, =>  UID int[]{4,1,0,1}
                 * */
                //parse field (potentially with repeat)
                //field-index with repeat: 3(3)=>{3,3}, field-index without repeat: 3=>{3,-1}
                this.UID = ParseUID(DataElementIndexString.ToCharArray());

                //parse the value-matching regular expression
                //NB, simplify regex by enclose ^...$ if it's not supplied - meaning default to exact-match, 
                //the side-effect is, for find "ABC in string" the pattern needs to be specified as *ABC*
                if (ruleParts.Length > 1)
                {
                    string regexPattern = ruleParts[1].Trim();
                    DataElementValueMatchingString = regexPattern;
                    //automatically enclose starts-with and ends-with if it's not already supplied in original
                    if (!regexPattern.StartsWith("^") && !regexPattern.EndsWith("$"))
                    {
                        regexPattern = (string.IsNullOrEmpty(regexPattern) ? @"^$" : $"^({regexPattern})$");
                        //regexPattern = $"^({regexPattern})$";
                    }
                    this.Regex = new Regex(regexPattern);
                }
            }
        }

        public SelectorResult SelectFrom(HL7Segment segment, List<HL7DataElement> excludedRepeats)
        {
            var result = GetQualifiedElements(segment, excludedRepeats);
            return new SelectorResult(this, result);
        }

        //get all addressed data-elements from the segment, optional to filter qualified only (RegEx)
        private List<HL7DataElement> GetQualifiedElements(HL7Segment segment, List<HL7DataElement> excludedRepeats)
        {
            List<HL7DataElement> addressed = segment.GetAddressedElements(UID);

            //apply value-matching via regular expression

            List<HL7DataElement> qualified = new List<HL7DataElement>();
            foreach (var element in addressed)
            {
                //if the element is component and sub-comp 
                //and if it's not in the implied field-repeat
                //this element is not qualified
                if (element.Qualify(this, excludedRepeats))
                {
                    qualified.Add(element);
                }
            }
            //
            return qualified;
        }


        public override string ToString()
        {
            return $"{SegmentName}-{DataElementIndexString}{(ExlcudeMatched ? '!' : '=')}={DataElementValueMatchingString}";
        }

        //helper - constructing UID (0-based int[]) 
        //Constructed UID gets longer if more index levels are specified in the path.
        //NB, in "string-notation", segment index UID[0] is always set to un-specified(-1)
        //examples:  
        //5=> int[]{4}
        //5.1 =>  int[]{4,-1,0}, 
        //5(2).1.2 => int[]{4,1,0,1}
        public static int[] ParseUID(char[] addressChars)
        {
            if (addressChars == null) { return null; }

            //parse the array and mark the special-chars' positions
            int indexOfFirstDot = -1, indexOfSecondDot = -1, indexOfOpenBraket = -1, indexOfCloseBracket = -1;
            for (int i = 0; i < addressChars.Length; i++)
            {
                char c = addressChars[i];
                if (c == '.')
                {
                    if (indexOfFirstDot < 0) { indexOfFirstDot = i; }
                    else if (indexOfSecondDot < 0) { indexOfSecondDot = i; }
                    else { /*ignore invalid*/}
                }
                else if (c == '(')
                {
                    if (indexOfOpenBraket < 0) { indexOfOpenBraket = i; }
                }
                else if (c == ')')
                {
                    if (indexOfCloseBracket < 0) { indexOfCloseBracket = i; }
                }
            }

            //construct each address element
            int[] fieldRepeatIndexes1 = null;
            int componentIndex1 = -1, subComponentIndex1 = -1;

            int fieldRepeatEndingIndex = addressChars.Length - 1; //set default field-ending position

            //if first-dot presents, parse component index
            if (indexOfFirstDot >= 0)
            {
                int componentIntegerEndingIndex = addressChars.Length - 1;  //default component-ending position

                //if second dot present, parse sub-component index
                if (indexOfSecondDot >= 0)
                {
                    //parse sub-comp index
                    subComponentIndex1 = ParseInt(addressChars, indexOfSecondDot + 1, addressChars.Length - 1);

                    //adjust component-ending position
                    componentIntegerEndingIndex = indexOfSecondDot - 1;
                }
                //parse comp-index
                componentIndex1 = ParseInt(addressChars, indexOfFirstDot + 1, componentIntegerEndingIndex);

                //adjust field-ending position
                fieldRepeatEndingIndex = indexOfFirstDot - 1;
            }

            //parse field-index and repeat-index
            fieldRepeatIndexes1 = ParseFieldRepeat(addressChars, fieldRepeatEndingIndex, indexOfOpenBraket, indexOfCloseBracket);

            //construct UID - NB, it converts 1-based index in rule-string to 0-based index in UID address
            if (subComponentIndex1 > 0)
            {
                return new int[] { fieldRepeatIndexes1[0] - 1, fieldRepeatIndexes1[1] - 1, componentIndex1 - 1, subComponentIndex1 - 1 };
            }
            else if (componentIndex1 > 0)
            {
                return new int[] { fieldRepeatIndexes1[0] - 1, fieldRepeatIndexes1[1] - 1, componentIndex1 - 1 };
            }
            else if (fieldRepeatIndexes1[1] > 0)
            {
                return new int[] { fieldRepeatIndexes1[0] - 1, fieldRepeatIndexes1[1] - 1 };
            }
            else
            {
                return new int[] { fieldRepeatIndexes1[0] - 1 };
            }
        }

        //helper - return a int[2]{field-index, repeat-index}
        private static int[] ParseFieldRepeat(char[] addressChars, int fieldRepeatEndingIndex, int indexOfOpenBraket, int indexOfCloseBracket)
        {
            try
            {
                int field, repeat = 0;
                if (indexOfOpenBraket > 0 && indexOfCloseBracket > indexOfOpenBraket)
                {
                    field = ParseInt(addressChars, 0, indexOfOpenBraket - 1);
                    repeat = ParseInt(addressChars, indexOfOpenBraket + 1, indexOfCloseBracket - 1);
                }
                else
                {   //no repeat-index
                    field = ParseInt(addressChars, 0, fieldRepeatEndingIndex);
                }
#if DEBUG

#endif
                return new int[] { field, repeat };
            }
            catch
            {
                throw new Exception($"ERROR parsing element indexes {new string(addressChars)}");
            }

        }

        internal bool Match(HL7DataElement element)
        {
            if (Regex != null)
            {
                return Regex.IsMatch(element.Value) ^ ExlcudeMatched;
            }

            return true;
        }

        //helper, indexes are inclusive
        private static int ParseInt(char[] addressChars, int integerStartingIndex, int integerEndingIndex)
        {
            return Int32.Parse(new string(addressChars, integerStartingIndex, integerEndingIndex - integerStartingIndex + 1).Trim());
        }


        //represent a collection of data-elements by applying a selector (UID+filter) to a segment
        public class SelectorResult
        {
            List<HL7DataElement> _selected = new List<HL7DataElement>();
            SegmentDataElementSelector Selector { get; } = null;


            internal List<HL7DataElement> GetQualifiedDataElements(List<HL7DataElement> exclusionContext, List<HL7DataElement> previouslySelectedRowElements)
            {
                List<HL7DataElement> qualifiedInContext = new List<HL7DataElement>();
                foreach (var element in _selected)
                {
                    if (!IsContextExcluded(element, exclusionContext))
                    {
                        //check against previously selected elements

                        //if there is a previous element in the same field, but in different repeat
                        //don't add this element to qualifiedInContext (=> exclude this element)
                        var thisRepeat = element.GetRepeat();
                        bool excludeThis = false;
                        if (thisRepeat != null /* e is repeat-level or below */ && previouslySelectedRowElements != null)
                        {
                            foreach (var prev in previouslySelectedRowElements)
                            {
                                //excldue this element if it's in the same field previously but in a different repeat
                                if (prev.GetField() == element.GetField() && prev.GetRepeat() != thisRepeat)
                                {
                                    excludeThis = true;
                                    break;
                                }
                            }
                        }

                        if (!excludeThis)
                        {
                            qualifiedInContext.Add(element);
                        }
                        //if (!e.IsVirtual)
                        //{
                        //}
                        //else if (includeVirtual)
                        //{
                        //    qualifiedInContext.Add(e);
                        //}
                    }
                }

                return qualifiedInContext;
            }

            public List<HL7DataElement> GetQualifiedDataElements(List<HL7DataElement> exclusionContext)
            {
                return GetQualifiedDataElements(exclusionContext, null);
            }

            private bool IsContextExcluded(HL7DataElement element, List<HL7DataElement> disqualifiedAndExcluded)
            {
                if(disqualifiedAndExcluded== null) { return false; }
                foreach (var e in disqualifiedAndExcluded)
                {
                    if (element == e) { return true; }
                }
                return false;
            }

            public SelectorResult(SegmentDataElementSelector selector, List<HL7DataElement> result)
            {
                Selector = selector;
                _selected = result;
            }

        }
    }
}
