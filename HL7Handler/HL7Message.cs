
using Charian;
using Foldda.Automation.Framework;
using Foldda.Automation.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Foldda.Automation.HL7Handler
{
    public class HL7Message : IRda
    {
        /* About .NET Event-handling ..https://stackoverflow.com/questions/803242/understanding-events-and-event-handlers-in-c-sharp */
        /* the "event client" (this) does 2 things : 1-declare the event, 2- raise the event (eg, in the Log() method) telling something needs to be done ... by handler(s) */
        /* (else-where, that is, anywhere has reference to a client-object, "event server/handler" do 2 things : 1-implement the even-handling delegate, 2-register to be interested to this client's raised event */

        public const char SEGMENT_SEPARATOR_CHAR = '\r';
        public static char[] DEFAULT_HL7_ENCODING_CHARS = new char[] { '|', '^', '~', '\\', '&' };


        public Encoding TextEncoding { get; set; }

        public HL7Segment GetFirstSegment(string segName)
        {
            return Segments.Where(x => x.Name.Equals(segName)).First();
        }

        public List<HL7Segment> GetSegments(string segName)
        {
            return Segments.Where(x => x.Name.Equals(segName)).ToList();
        }
        
        public string TypeExt => ".hl7";

        public static HL7Message Parse(char[] data, Encoding encoding)
        {
            HL7Message result = null;
            if(data==null | data.Length > 8)
            {
                try
                {
                    result = new HL7Message(data, encoding);
                }
                catch { }
            }

            return result;
        }

        HL7MessageEncoding MessageEncoding => MSH?.MessageEncoding ?? HL7MessageEncoding.Default;

        public HL7Message(char[] data, Encoding encoding)
        {
            this.TextEncoding = encoding;
            //this.SetLoggingHandler(logger); //child has a reference to parent/hosting class, and that's OK.
            
            StringBuilder buffer = new StringBuilder();
            Segments.Clear();
            foreach (char c in data)
            {
                if (c != SEGMENT_SEPARATOR_CHAR)
                {
                    buffer.Append(c);
                }
                else
                {
                    string segmentString = buffer.ToString().Trim();

                    //first segment is MSH and it needs to be MshSegmant 
                    if(Segments.Count == 0)
                    {
                        Segments.Add(new MshSegment(segmentString));
                    }
                    else
                    {
                        Segments.Add(new HL7Segment(MessageEncoding, segmentString));
                    }
                   
                    buffer.Clear();
                }
            }

            string lastPotentialSegment = buffer.ToString().Trim();
            if (lastPotentialSegment.Length > 4)
            {   //
                Segments.Add(new HL7Segment(MessageEncoding, lastPotentialSegment));
                buffer.Clear();
            }
        }

        public HL7Message(Rda record)
        {
            this.FromRda(record);
        }

        public char[] ToChars()
        {
            StringBuilder allChars = new StringBuilder();

            foreach(HL7Segment segment in Segments)
            {
                allChars.Append(segment.Value).Append(SEGMENT_SEPARATOR_CHAR);
            }
            //NB, we keep the last \r
            char[] result = new char[allChars.Length];
            allChars.CopyTo(0, result, 0, result.Length);
            return result;
        }

        public override string ToString()
        {
            return new string(ToChars());
        }

        public byte[] ToBytes()
        {
            return TextEncoding.GetBytes(ToChars());
        }

        //enclosing the HL7 message content in MLLP starting and ending bytes
        public byte[] ToMllpBytes()
        {
            char[] hl7 = this.ToChars();
            char[] chars = new char[hl7.Length + 3];
            chars[0] = '\v';
            for (int i = 0; i < hl7.Length; i++) { chars[i + 1] = hl7[i]; }
            chars[chars.Length - 2] = (char)0x1c;
            chars[chars.Length - 1] = '\r';

            return TextEncoding.GetBytes(ToChars());
        }

        //All Segments including the MSH segment
        public List<HL7Segment> Segments { get; } = new List<HL7Segment>();

        public MshSegment MSH => Segments.Count > 0 ? (MshSegment)Segments[0] : null;

        public int[] UID => new int[0] ;

        public List<HL7DataElement> GetAddressedElements(string segmentName, int[] elementIndexes)
        {
            List<HL7DataElement> result = new List<HL7DataElement>();

            foreach(var segment in GetSegments(segmentName))
            {
                result.AddRange(segment.GetAddressedElements(elementIndexes));
            }

            return result;
        }

        public HL7DataElement GetFirstAddressedElement(string segmentName, int[] elementIndexes)
        {
            return GetAddressedElements(segmentName, elementIndexes).FirstOrDefault();
        }


        //returns the number of records affected
        public int SetDataElementsValueByPath(string segmentName, int[] elementIndexes, string value)
        {
            var matchedElements = GetAddressedElements(segmentName, elementIndexes);
            foreach(var element in matchedElements)
            {
                element.ScalarValue = value;
            }

            return matchedElements.Count;
        }

        public string UnEscape(string escaped)
        {
            if (string.IsNullOrEmpty(escaped)) { return escaped; }

            return escaped
                .Replace(@"\F\", MessageEncoding.FieldSeparator.ToString())
                .Replace(@"\S\", MessageEncoding.ComponentSeparator.ToString())
                .Replace(@"\R\", MessageEncoding.RepeatSeparator.ToString())
                .Replace(@"\T\", MessageEncoding.SubComponentSeparator.ToString())
                .Replace(@"\E\", MessageEncoding.EscapeChar.ToString());
        }

        public string Escape(string oringinal)
        {
            if (string.IsNullOrEmpty(oringinal)) { return oringinal; }

            return oringinal
                .Replace(MessageEncoding.EscapeChar.ToString(), @"\E\")
                .Replace(MessageEncoding.FieldSeparator.ToString(), @"\F\")
                .Replace(MessageEncoding.ComponentSeparator.ToString(), @"\S\")
                .Replace(MessageEncoding.RepeatSeparator.ToString(), @"\R\")
                .Replace(MessageEncoding.SubComponentSeparator.ToString(), @"\T\");
        }

        public Rda ToRda()
        {
            Rda result = new Rda();

            foreach(var segment in Segments)
            {
                result.Elements.Add(segment.ToRda());
            }
            return result;
        }

        public IRda FromRda(Rda rda)
        {
            Segments.Add(new MshSegment(rda[0]));

            for (int i = 1; i < rda.Elements.Count; i++)
            {
                Segments.Add(new HL7Segment(MessageEncoding, rda[i]));
            }

            return this;
        }

        public class HL7MessageEncoding : IContainerRecordEncoding
        {
            public char[] EncodingChars { get; private set; } = new char[] { '|', '^', '~', '\\', '&' };  //HL7 Default encoding
            public char SegmentSeparator => '\r';
            public char EscapeChar => EncodingChars[3];

            public static HL7MessageEncoding Default = new HL7MessageEncoding();

            private HL7MessageEncoding() { }

            public static HL7MessageEncoding Parse(string mshSegmentString)
            {
                string mshHeader = mshSegmentString?.Trim();
                if (string.IsNullOrEmpty(mshHeader) || mshHeader.Length < 9 || !mshHeader.StartsWith("MSH"))
                {
                    throw new Exception($"Invalid MSH segment - '{mshSegmentString}'");
                }

                return new HL7MessageEncoding(mshHeader.Substring(3, 1), mshHeader.Substring(4, 4));
            }

            public virtual IRda Parse(char[] recordChars)
            {
                return new HL7Message(recordChars, TextEncoding); ;
            }

            public string MSH_1 { get; }
            public string MSH_2 { get; }

            internal HL7MessageEncoding(string msh_1, string msh_2)
            {
                //TODO add validation here - eg no repeating chars
                MSH_1 = msh_1;
                MSH_2 = msh_2;

                EncodingChars = (msh_1 + msh_2).ToCharArray();
            }

            internal char GetSeparator(Level0 childLevel)
            {
                switch (childLevel)
                {
                    case Level0.Field: { return EncodingChars[0]; }
                    case Level0.Repeat: { return EncodingChars[2]; }
                    case Level0.Component: { return EncodingChars[1]; }
                    case Level0.SubComponent: { return EncodingChars[4]; }
                    default: { return '\0'; }
                }
            }

            //Used in Rda transporting for re-construct HL7MessageEncoding
            public override string ToString()
            {
                char[] s = new char[4];
                EncodingChars.CopyTo(s, 1);
                return new string(s);
            }


            public char[] Encode(IRda record)
            {
                return (new HL7Message(record.ToRda())).ToString().ToCharArray();
            }

            /// <summary>
            /// Encode the Rda record container's header, if applicable, eg a HL7 "batch header that at the start of multiple HL& messages
            /// </summary>
            /// <param name="containerMetaData">The container's meta data</param>
            /// <returns>the char[] to be attached to the beginning of the encoded container records string, NULL if N/A</returns>
            public char[] EncodeContainerHeader(Rda containerMetaData)
            {
                return null;
            }

            /// <summary>
            /// Encode the Rda record container's trailer, if applicable, eg an XML file's document-ending tag
            /// </summary>
            /// <param name="containerMetaData">The container's meta data</param>
            /// <returns>the char[] to be appended at the end to the encoded container records string, NULL if N/A</returns>
            public char[] EncodeContainerTrailer(Rda containerMetaData)
            {
                return null;
            }

            /// <summary>
            /// The separator char(s) used for separating encoded records in a continous string.
            /// </summary>
            public char[] RecordSeparator { get; set; } = new char[] {'\r', '\n' };

            public char FieldSeparator => GetSeparator(Level0.Field);
            public char ComponentSeparator => GetSeparator(Level0.Component);
            public char RepeatSeparator => GetSeparator(Level0.Repeat);
            public char SubComponentSeparator => GetSeparator(Level0.SubComponent);

            public Encoding TextEncoding { get; set; } = Encoding.Default;
        }



        public class HL7MessageScanner : AbstractCharStreamRecordScanner
        {
            static readonly char[] MSH_DEFAULT = new char[] { 'M', 'S', 'H', '|', '^', '~', '\\', '&', '|' };
            /**
             * Matcher matches the given pattern in the stream, if found, it notify the caller
             * by returning the matched pattern (char[])
             */
            CharsArrayRecordMatcher _matcher;

            public HL7MessageScanner(ILoggingProvider logger, HL7MessageEncoding hl7Encoding) : this(logger, MSH_DEFAULT) 
            {
                RecordEncoding = hl7Encoding;
            }
            public HL7MessageScanner() : this(null, MSH_DEFAULT) { }

            public HL7MessageScanner(ILoggingProvider logger, char[] customMSH) : base(logger)
            {
                if (customMSH == null || customMSH.Length != 9)
                {
                    throw new Exception($"Unexpected message header supplied. Correct format must start with MSH followed by separator-chars, eg - \"MSH|^~\\&|\" or \"MSH;,-+$;\"");
                }
                _matcher = new CharsArrayRecordMatcher(customMSH);
            }


            // a simplified, synchronised warpper version of HL7-message scanning
            // returns hl7 records from scanning the input.
            public static List<char[]> Scan(char[] input, Encoding encoding)
            {
                HL7MessageScanner scanner = new HL7MessageScanner();
                List<char[]> result = new List<char[]>();
                scanner.ScanRecordsInStreamAsync(input, encoding, new System.Threading.CancellationToken()).Wait();
                while (scanner.HarvestedRecords.TryTake(out var hl7))
                {
                    result.Add(hl7);
                }

                return result;
            }

            protected override async Task ScanStreamReadBufferAsync(char[] readBuffer, int dataLengthInBuffer)
            {
                //builds messages and store them into bufferScanResult, one char[] per message
                //rule: message start with MSH, end with next MSH or when Collect() is called.

                /* if no more incoming data from source */
                for (int pos = 0; pos < dataLengthInBuffer; pos++)
                {
                    char c = readBuffer[pos];
                    char[] record = _matcher.AppendAndFetch(c);
                    if (record != null)
                    {
                        await ValidateCleansAndSave(record);
                    }
                }
            }

            //ideally record contains a clean HL7 message, however we do a round of cleansing here, around the '\r' chars
            //return any "unused chars", so the matcher can choose "to put it back to buffer" or "to disgard"
            private async Task ValidateCleansAndSave(char[] record)
            {
                int matchHeaderLength = _matcher.RecordMarkerChars.Length;
                //record must at least has [MSH|....]
                if (record.Length < matchHeaderLength)
                {
                    Log($"Skipped {record.Length} chars: '{new string(record)}'.");
                    return;
                }
                else if (record[0] != 'M' || record[1] != 'S' || record[2] != 'H')
                {
                    Log($"Record [{record}] invalid.");
                    return;
                }

                //MSH header is assumed present..
                char fieldSeparatorChar = record[3];   //MSH separator char position
                StringBuilder cleansingBuffer = new StringBuilder(new string(_matcher.RecordMarkerChars));
                int i = matchHeaderLength;
                while (i < record.Length)
                {
                    char c = record[i];
                    //if c==\r, match CCC|, skip control-char and space in-between
                    if (Char.IsControl(c))
                    {
                        if (c == '\r')
                        {
                            //find the next printable char ...
                            while (++i < record.Length)
                            {
                                if (!Char.IsControl(record[i]) && !Char.IsWhiteSpace(record[i]))
                                {
                                    break;
                                }
                            }

                            if (i == record.Length)
                            {
                                break; //EOR reach, skip this \r and any blanks after
                            }
                            else if ( //else, check the formality of the next segment header
                                    i + 3 < record.Length &&
                                    record[i + 3] == fieldSeparatorChar &&
                                    Char.IsUpper(record[i]) &&
                                    Char.IsUpper(record[i + 1]) &&
                                    (Char.IsUpper(record[i + 2]) || Char.IsDigit(record[i + 2]))
                                )
                            {
                                //it's fine, add \r and the next segment header - 
                                cleansingBuffer.Append('\r').Append(record[i]).Append(record[i + 1]).Append(record[i + 2]).Append(fieldSeparatorChar);

                                i += 4;  //skip segment-header chars to scan fields
                                continue;
                            }
                            else
                            {
                                //the chars following the \r do not match a segment pattern, convert the \r to a space
                                cleansingBuffer.Append(' ');
                            }
                        }
                        else if (c != '\t')
                        {
                            cleansingBuffer.Append(' '); //replace control-char (except /r & /t) to space
                        }
                        //else .. other control chars are stripped.
                    }
                    else
                    {
                        //non-control chars - append as is..
                        cleansingBuffer.Append(record[i]);
                    }

                    i++;
                }

                //record is validated and cleansed, now save 
                await AddToCollectionAsync(cleansingBuffer.ToString().ToCharArray());
            }

            protected override async Task HarvestBufferedRecords()
            {
                await ValidateCleansAndSave(_matcher.FetchCurrentlyBufferred());
            }

            public override IRda Parse(char[] hl7RecordChars, Encoding encoding)
            {
                return new HL7Message(hl7RecordChars, encoding);
            }
        }
    }
}
