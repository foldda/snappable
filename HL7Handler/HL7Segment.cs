using Charian;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.HL7Handler
{
    public class HL7Segment : IRda //: DataElement
    {
        //public List<Field> Fields { get { return ChildElements.Cast<Field>().ToList(); } }
        public List<HL7DataElement> Fields { get; } = new List<HL7DataElement>();

        public HL7Message.HL7MessageEncoding MessageEncoding { get; protected set; }

        public virtual string Name { get; set; }

        //public MessageEncoding MessageEncoding { get; protected set; }

        public HL7Segment(HL7Message.HL7MessageEncoding hl7Encoding, Rda segmentRda)// : base(null, new int[0], value)
        {
            MessageEncoding = hl7Encoding;
            FromRda(segmentRda);
        }

        public HL7Segment(HL7Message.HL7MessageEncoding hl7Encoding, string segmentString)// : base(null, new int[0], value)
        {
            string[] tokens = segmentString.Split(new char[] { hl7Encoding.GetSeparator(Level0.Field) });
            Name = tokens[0];
            MessageEncoding = hl7Encoding;

            for (int i = 1; i < tokens.Length; i++)
            {
                Fields.Add(new HL7DataElement(null, this, tokens[i]));
            }
        }

        public override string ToString()
        {
            return Value;
        }

        internal virtual List<HL7DataElement> GetAddressedElements(int[] elementIndexes)
        {
            if(elementIndexes != null && elementIndexes.Length > (int)Level0.Field && elementIndexes[(int)Level0.Field] < Fields.Count)
            {
                return Fields[elementIndexes[(int)Level0.Field]].GetMatchedElements(elementIndexes); 
            }
            else
            {
                return new List<HL7DataElement>();
            }
        }

        public virtual string Value => Name + MessageEncoding.FieldSeparator 
            + string.Join(MessageEncoding.FieldSeparator.ToString(), Fields.Select(childElement => childElement.Value).ToList());

        //just capture the data values without the encoding, which would depend on (be supplied by) the HL7 Messsage constructor
        public virtual Rda ToRda()
        {
            Rda result = new Rda();
            result.Elements.Add(new Rda() { ScalarValue = Name });

            foreach(var f in Fields)
            {
                result.Elements.Add(f.ToRda());
            }

            return result;
        }

        public virtual IRda FromRda(Rda segmentRda)
        {
            Name = segmentRda[0].ScalarValue;

            for (int i = 1; i < segmentRda.Elements.Count; i++)
            {
                Fields.Add(new HL7DataElement(this, null, segmentRda[i]));
            }

            return this;
        }
    }
 
    public class MshSegment : HL7Segment
    {
        public MshSegment(Rda rda) : base(new HL7Message.HL7MessageEncoding(rda[1].ScalarValue, rda[2].ScalarValue),  rda)
        {
        }

        //public MshSegment(Rda mshRda) : base(null, mshRda)
        //{
        //    //MessageEncoding is constructed in the FromRda() override 
        //}

        internal MshSegment(string segmentString) 
            //here we hack the parsing of MSH value by adding an extra '|' to the "value" (at the encoding field),
            //so under 'normal parsing', it'll fake a MSH-1 whilst pusing MSH-2 onwards to the right place 
            : base(HL7Message.HL7MessageEncoding.Parse(segmentString), segmentString.Substring(0, 9) + segmentString.Substring(8)) 
        {

            //after the above hack, through the default parsing, MSH-1 (Fields[0]) would be blank,
            //now we hard-code its value to be the separator char - as per the HL7 Standard.
            Fields[0].ScalarValue = MessageEncoding.MSH_1;

            //for MSH-2, the default parsing will break the field into multiple (levels of) sub-elements - i.e. a composite data type
            //here we hard code the value (HL7 encoding chars) as a 'scalar' value
            Fields[1].ScalarValue = MessageEncoding.MSH_2;
        }

        public override string Value
        {
            get
            {
                //Fields.Skip(1) because MSH_1 joins MSH_2 without the field-separator char
                return Name + MessageEncoding.MSH_1 + string.Join(MessageEncoding.FieldSeparator.ToString(), Fields.Skip(1).Select(item => item.Value).ToArray());
            }
        }

        //just capture the data values without the encoding, which would depend on (be supplied by) the HL7 Messsage constructor
        public override Rda ToRda()
        {
            Rda result = new Rda();
            result.Elements.Add(new Rda() { ScalarValue = Name });

            for (int i = 0; i < Fields.Count; i++)
            {
                if (i == 0)
                {
                    //MSH-1
                    result.Elements.Add(new Rda() { ScalarValue = MessageEncoding.MSH_1 });
                }
                else if (i == 1)
                {
                    //MSH-2
                    result.Elements.Add(new Rda() { ScalarValue = MessageEncoding.MSH_2 });
                }
                else
                {
                    result.Elements.Add(Fields[i].ToRda());
                }
            }

            return result;
        }

        //public override IRda FromRda(Rda segmentRda)
        //{
        //    MessageEncoding = new HL7MessageEncoding(segmentRda[1].ScalarValue, segmentRda[2].ScalarValue);

        //    return base.FromRda(segmentRda);
        //}

    }
}
