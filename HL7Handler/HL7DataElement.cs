using Charian;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Foldda.DataAutomation.HL7Handler
{
    public enum Level0 : int { Segment = -1, Field = 0, Repeat = 1, Component = 2, SubComponent = 3 }

    public class HL7DataElement : IRda
    { 
        protected HL7DataElement() { }

        //element level is parent's level + 1
        public Level0 ElementLevel => (ParentElement?.ElementLevel ?? Level0.Segment) + 1;

        protected List<HL7DataElement> ChildElements { get; set; } = new List<HL7DataElement>();

        protected string _value = string.Empty;

        //index as in the path UID
        internal int SibilingIndex
        {
            get
            {
                if(ElementLevel == Level0.Field)
                {
                    return Segment.Fields.IndexOf(this);
                }
                else
                {
                    return ParentElement.ChildElements.IndexOf(this);
                }
            }
        }

        public HL7Message.HL7MessageEncoding MessageEncoding => Segment.MessageEncoding;

        public string Value => ElementLevel == Level0.SubComponent || ChildElements.Count == 0 ? _value 
            : string.Join(ChildElementSeparator.ToString(), ChildElements.Select(childElement => childElement.Value).ToList());

        public char ChildElementSeparator => MessageEncoding.GetSeparator(ElementLevel + 1);    //+1 => child-level

        protected void FromHl7EncodedString(string hl7EncodedString)
        {
            if (ElementLevel > Level0.SubComponent || ElementLevel < Level0.Segment)
            {
                throw new Exception($"ElementLevel [{ElementLevel}] is invalid.");
            }
            else if (ElementLevel == Level0.SubComponent)
            {
                _value = hl7EncodedString;
            }            
            else
            {
                foreach (var childHl7String in hl7EncodedString.Split(new char[] { ChildElementSeparator }))
                {
                    ChildElements.Add(new HL7DataElement(this, Segment, childHl7String));
                }
            }
        }

        public HL7DataElement (HL7DataElement parent, HL7Segment hl7Segment, string hl7ElementEncodedString)
        {
            ParentElement = parent;
            ChildElements.Clear();
            Segment = hl7Segment;

            //fills the ChildElements collection by parsing the hl7EncodedString
            //.. which incurs **recursive** construction of DataElement objects.
            FromHl7EncodedString(hl7ElementEncodedString);
        }
        public HL7Segment Segment { get; } = null;

        public HL7DataElement ParentElement { get; private set;} = null;

        public HL7DataElement (HL7Segment segment, HL7DataElement parentDataElement, Rda dataElementRda)
        {
            Segment = segment;
            ParentElement = parentDataElement;
            FromRda(dataElementRda);
        }

        public Rda ToRda()
        {
            Rda result = new Rda();

            if (ElementLevel < Level0.SubComponent)
            {
                foreach (var childElement in ChildElements)
                {
                    result.Elements.Add(childElement.ToRda());
                }
            }
            else
            {
                result.ScalarValue = _value;
            }

            return result;
        }

        public IRda FromRda(Rda deRda)
        {
            ChildElements.Clear();
            if (ElementLevel < Level0.SubComponent && deRda.Elements.Count > 0)
            {
                foreach (var childElementRda in deRda.Elements)
                {
                    ChildElements.Add(new HL7DataElement(Segment, this, childElementRda));  //recurrsion
                }
            }
            else
            {
                _value = deRda.ScalarValue;
            }
            return this;
        }

        /// <summary>
        /// ///
        /// </summary>

        public bool IsVirtual { get; internal set; } = false;

        public override string ToString()
        {
            return Value;
        }

        public string ScalarValue 
        {
            get 
            { 
                return ChildElements.Count > 0 ? ChildElements[0].ScalarValue : _value; 
            }

            internal set 
            { 
                ChildElements.Clear(); 
                _value = value; 
            }
        }

        internal List<HL7DataElement> GetMatchedElements(int[] elementIndexes)
        {
            List<HL7DataElement> result = new List<HL7DataElement>();
            int addressedLevel = elementIndexes.Length - 1;

            //if the addressed level goes beyond this level, then 
            if(addressedLevel > (int)this.ElementLevel)
            {
                //we'll need to check child-elements matches, but only if "this level is a match", i.e. ..
                if(elementIndexes[(int)this.ElementLevel] == SibilingIndex || elementIndexes[(int)this.ElementLevel] == -1)
                {
                    foreach (var childElement in ChildElements)
                    {
                        result.AddRange(childElement.GetMatchedElements(elementIndexes)); //recursion
                    }
                }
                //else
            }
            else if(addressedLevel == (int)this.ElementLevel && elementIndexes[(int)this.ElementLevel] == SibilingIndex)
            {
                result.Add(this);
            }

            return result;
        }

        //if this element is not qualified by the selector, add its repeat to the exlusion list, so
        //the whole repeat (and its sub-elements) won't get selected in the future qualification selection
        //internal virtual bool Qualify(SegmentDataElementSelector selector, List<HL7DataElement> excludedRepeats)
        //{
        //    HL7DataElement thisRepeat = this.GetRepeat();
        //    if (excludedRepeats != null && excludedRepeats.Contains(thisRepeat))
        //    {
        //        return false;
        //    }

        //    if (selector.Match(this))
        //    {
        //        return true;
        //    }
        //    else
        //    {
        //        excludedRepeats?.Add(thisRepeat);
        //        return false;
        //    }
        //}


        /// <summary>
        /// check if this element "qualifies" the given path (matches at its index also at all parents' indexes)
        /// the matching starts from the end, then upwards towards each parents, returns true is all matches.
        /// NB, -1 in the targeted index means 'wildcard' which matches everything
        /// 
        /// Level0.Segment = -1
        /// Level0.Field = 0
        /// Level0.Repeat = 1
        /// Level0.Component = 2
        /// </summary>
        /// <param name="targettedElementIndexes"></param>
        /// <returns>true if the targetted index matches this element's 'sibiling index' at this level</returns>
        //private bool IndexQualifies(int[] targettedElementIndexes)
        //{
        //    if ((int)ElementLevel >= targettedElementIndexes.Length) { return false; }

        //    //else..
        //    int targettedIndex = targettedElementIndexes[(int)ElementLevel];

        //    //this element's index among its sibilings (parent element's children)
        //    return (SibilingIndex == targettedIndex || targettedIndex == -1);
        //}

        internal HL7DataElement GetExcludingElement()
        {
            if (ElementLevel == Level0.SubComponent) { return ParentElement.ParentElement; }
            else if (ElementLevel == Level0.Component) { return ParentElement; }
            else { return this; }
        }

        //if this element is not qualified by the selector, add its repeat to the exlusion list, so
        //the whole repeat (and its sub-elements) won't get selected in the future qualification selection
        internal virtual bool Qualify(SegmentDataElementSelector selector, List<HL7DataElement> excludedRepeats)
        {
            HL7DataElement thisRepeat = this.GetExcludingElement();
            if (excludedRepeats != null && excludedRepeats.Contains(thisRepeat))
            {
                return false;
            }

            if (selector.Match(this))
            {
                return true;
            }
            else
            {
                excludedRepeats?.Add(thisRepeat);
                return false;
            }
        }

        public HL7DataElement GetRepeat()
        {
            switch (ElementLevel)
            {
                case Level0.Repeat: { return this; }
                case Level0.Component: { return ParentElement; }
                case Level0.SubComponent: { return ParentElement.ParentElement; }
                default: { return null; }   //segment or field
            }
        }

        internal HL7DataElement GetField()
        {
            switch (ElementLevel)
            {
                case Level0.Field: { return this; }
                case Level0.Repeat: { return ParentElement; }
                case Level0.Component: { return ParentElement.ParentElement; }
                case Level0.SubComponent: { return ParentElement.ParentElement.ParentElement; }
                default: { return null; }   //segment
            }
        }


        /* 
* PID-5.1==xyz|abc   or   PID-5.2!=123
*/
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

            public SegmentDataElementSelector(string selectorString /* eg PID-5 */)
            {
                //determine excludion-flag based on presence of !=
                ExlcudeMatched = (selectorString.IndexOf(NOT_EQUAL_OPERATOR) >= 0);

                //construct the conditional RegEx, based on splitting (!= or ==)
                string[] ruleParts = selectorString.Split(
                    new string[] { EQUAL_OPERATOR, NOT_EQUAL_OPERATOR }, StringSplitOptions.RemoveEmptyEntries);

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
                    return Regex.IsMatch(element.Value) && !ExlcudeMatched;
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






        ///**
        // * int[]{field-index, repeat-index, ...} for the segment's children elements 
        // * NB, for addressing, UID.repeat-index can be widlcard, i.e. -1
        // */
        //internal List<DataElement> GetAddressedElements(int[] addressCanBeWildcard)
        //{
        //    if(addressCanBeWildcard==null)
        //    {
        //        return new List<DataElement>() { this };
        //    }
        //    else if (addressCanBeWildcard.Length == ElementId.Length)
        //    {
        //        //if address-matching level is the same as me (this element, which exists), that's easy .. 
        //        if (this.AddressMatched(addressCanBeWildcard)) { return new List<DataElement> { this }; }
        //        else { return null; }
        //    }
        //    else if (addressCanBeWildcard.Length > ElementId.Length)
        //    {
        //        //..else, if the address is longer than mine, it could be refer to for one of my children ..
        //        //so we check the leading part of the supplied address against mine first ...
        //        for (int addressIndexLevel = 0; addressIndexLevel < ElementId.Length; addressIndexLevel++)
        //        {
        //            if (addressIndexLevel == (int)Level0.Repeat && addressCanBeWildcard[addressIndexLevel] == -1)
        //            {   //widecard repeat matching always passes
        //                continue;
        //            }
        //            else if (ElementId[addressIndexLevel] != addressCanBeWildcard[addressIndexLevel])
        //            {
        //                return null;    //no, it's not for my "family" - leading part of the address doesn't match mine
        //            }
        //        }

        //        //here , it has passed the "family check", so we will recurrsively check all my children .. 

        //        // and if all this element's UID address matches the given address, 
        //        // find the next level addressed elements in the children (by recursion), 
        //        //.. but first check if the child exists, or (if not) the family needs to be extedned
        //        //NB, it's direct child, not grandchidlren
        //        int intendedDirectChildIndex = addressCanBeWildcard[ElementId.Length];
        //        if (intendedDirectChildIndex < 0 /* wildcard, no intended child = every child matched at the next level */)
        //        {
        //            List<DataElement> aggregateResult = new List<DataElement>();
        //            foreach (var child in ChildElements)
        //            {
        //                //recursion ...
        //                var childRes = child.GetAddressedElements(addressCanBeWildcard);
        //                if (childRes != null && childRes.Count > 0)
        //                {
        //                    aggregateResult.AddRange(childRes);
        //                }
        //            }
        //            return aggregateResult.Count > 0 ? aggregateResult : null;
        //        }
        //        else
        //        {
        //            //intendedChildIndex >= 0, meaning the supplied address is intended for one specific child
        //            if (ChildElements.Count <= intendedDirectChildIndex)
        //            {
        //                //create addtional child elements until the required is created
        //                while (ChildElements.Count <= intendedDirectChildIndex)
        //                {
        //                    //prepare the new child's UID - beginning part is the same as mine ..
        //                    int[] newChildUID = new int[ElementId.Length + 1];
        //                    for (int i = 0; i < ElementId.Length; i++) { newChildUID[i] = ElementId[i]; }

        //                    //child-index as the "last being added"
        //                    newChildUID[ElementId.Length] = ChildElements.Count;

        //                    //make the new child, mark it as "virtual"
        //                    var newChild = this.MakeChildElement(newChildUID, string.Empty);
        //                    newChild.IsVirtual = true;

        //                    //add the new child .. this increase the ChildElements.Count
        //                    ChildElements.Add(newChild);

        //                    //.. we keep adding virtual children until the "addressed child" is created.
        //                }
        //            }

        //            //now we have got the addressed child (which can be virtual)
        //            //recursively (find) into the child
        //            var intendedChild = ChildElements[intendedDirectChildIndex];
        //            return intendedChild.GetAddressedElements(addressCanBeWildcard);
        //        }
        //    }
        //    else //addressCanBeWildcard.Length < UID.Length) -- supplied address is for parent-matching, not for me
        //    {
        //        return null;
        //    }
        //}



        //private bool AddressMatched(int[] address)
        //{
        //    if (ElementId.Length != address.Length) { return false; }

        //    //every level index must match, with expection that address[repeat-index] is -1 'widlcard'
        //    switch (this.ElementLevel)
        //    {
        //        //case Level0.Segment:
        //        //    { return true; }
        //        case Level0.Field/*{UID[0]}*/:
        //            { return ElementId[0] == address[0]; }
        //        case Level0.Repeat/*{UID[0],UID[1]}*/:
        //            { return ElementId[0] == address[0] && (address[1] == -1 || ElementId[1] == address[1]); }
        //        case Level0.Component/*{UID[0],UID[1],UID[2]}*/:
        //            { return ElementId[0] == address[0] && (address[1] == -1 || ElementId[1] == address[1]) && ElementId[2] == address[2]; }
        //        case Level0.SubComponent/*{UID[0],UID[1],UID[2],UID[3]}*/:
        //            { return ElementId[0] == address[0] && (address[1] == -1 || ElementId[1] == address[1]) && ElementId[2] == address[2] && ElementId[3] == address[3]; }
        //        default: { return false; }
        //    }
        //}
    }

    ////no child elements, has value local var
    //public class SubComponent : DataElement
    //{
    //    string _value = string.Empty;



    //    public SubComponent(DataElement parent, int[] UID, string value) : base(parent, UID, value) { }

    //    public override MessageRepeat GetRepeat() { return (MessageRepeat) this.Parent.Parent; }

    //    public override MessageField GetField() { return (MessageField) this.Parent.Parent.Parent; }


    //    protected override DataElement MakeChildElement(int[] childUID, string value) { return null; } //this is not called

    //    public override string Value
    //    {
    //        /*
    //         * LESSON: don't do escape/unescape at this (encoding) level, do it at the application/client-level where it's consumed
    //         * That's because if setter does the escaping, it messes up an already escaped value
    //         * if only do it at the getter, then it's inbalanced when accessing/transforming/re-encoding the value
    //         * 
    //         * Use the HL7Message.Unescape if requiered to consume the value.
    //         */
    //        get
    //        {
    //            return _value;
    //        }

    //        set
    //        {   //escaping HL7 separators here
    //            _value = value;
    //            //Log($"Setting value [{value}] as [{_value}] to element {PrintUID()}");
    //        }
    //    }
    //}

    //public class Component : DataElement
    //{
    //    public Component(DataElement parent, int[] UID, string value) : base(parent, UID, value) { }
    //    public List<SubComponent> SubComponents { get { return ChildElements.Cast<SubComponent>().ToList(); } }

    //    public override MessageRepeat GetRepeat() { return (MessageRepeat) this.Parent; }

    //    public override MessageField GetField() { return (MessageField) this.Parent.Parent; }

    //    protected override DataElement MakeChildElement(int[] childUID, string value) { return new SubComponent(this, childUID, value); }
    //}

    //public class MessageRepeat : DataElement
    //{
    //    public MessageRepeat(DataElement parent, int[] UID, string value) : base(parent, UID, value) { }
    //    public List<Component> Components { get { return ChildElements.Cast<Component>().ToList(); } }
    //    protected override DataElement MakeChildElement(int[] childUID, string value) { return new Component(this, childUID, value); }

    //    public override MessageRepeat GetRepeat() { return this;}

    //    public override MessageField GetField() { return (MessageField) this.Parent;}

    //    public override string ElementAddress
    //        => $"{Parent.ElementAddress}({ElementId[ElementId.Length - 1] + 1})";
    //}


}
