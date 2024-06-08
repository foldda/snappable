using Charian;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Foldda.DataAutomation.Framework;

using System.Threading.Tasks;

namespace Foldda.DataAutomation.CsvHandler
{
    public class TabularRecord : IRda
    {

        internal static readonly TabularRecordEncoding DEFAULT_RECORD_ENCODING = new TabularRecordEncoding();

        public List<string> ItemValues { get; } = new List<string>();

        public TabularRecord(List<string> csvRow)
        {
            ItemValues = csvRow.ToList();
        }

        public TabularRecord(Rda rda)
        {
            FromRda(rda);
        }

        public string ToString(TabularRecordEncoding encoding)
        {
            return new string(encoding.Encode(this));
        }

        public override string ToString()
        {
            return ToString(DEFAULT_RECORD_ENCODING);
        }

        public Rda ToRda()
        {
            Rda result = new Rda
            {
                ChildrenValueArray = ItemValues.ToArray()
            };

            return result;
        }

        public IRda FromRda(Rda rda)
        {
            ItemValues.Clear();
            foreach (string s in rda.ChildrenValueArray)
            {
                ItemValues.Add(s);
            }
            return this;
        }

        public static MetaData GetMetaData(Rda containerLabelRda)
        {
            return new MetaData(containerLabelRda);
        }

        public class MetaData : Rda
        {
            enum META_DATA : int { SOURCE, DESTINATION, COLUMN_NAMES, COLUMN_DATA_TYPES }  // also "VALIDATION_RULES"

            internal MetaData(Rda containerLabelRda)
            {
                FromRda(containerLabelRda);
            }

            public MetaData() : base() { }

            public string SourceId   //eg - the input CSV file name
            {
                get => this[(int)META_DATA.SOURCE].ScalarValue;
                set => this[(int)META_DATA.SOURCE].ScalarValue = value;
            }

            public string DestinationId   //eg - the targeted database table
            {
                get => this[(int)META_DATA.DESTINATION].ScalarValue;
                set => this[(int)META_DATA.DESTINATION].ScalarValue = value;
            }

            public string[] ColumnNames   //eg - the input CSV file name
            {
                get => this[(int)META_DATA.COLUMN_NAMES].ChildrenValueArray;
                set => this[(int)META_DATA.COLUMN_NAMES].ChildrenValueArray = value;
            }

            public string[] ColumnDataTypes   //eg - string description of the columns' data type eg 'int', 'date:yyyyMMdd'
            {
                get => this[(int)META_DATA.COLUMN_DATA_TYPES].ChildrenValueArray;
                set => this[(int)META_DATA.COLUMN_DATA_TYPES].ChildrenValueArray = value;
            }
        }

        public class TabularRecordEncoding : IContainerRecordEncoding
        {
            public static readonly TabularRecordEncoding Default = new TabularRecordEncoding();
            public static readonly char DOUBLE_QUOTE = '"';
            public static readonly char COMMA = ',';
            public static readonly char NULL_CHAR = '\0';

            public char DelimiterChar { get; } = COMMA;
            public char QualifierChar { get; } = DOUBLE_QUOTE;
            public char EscapeChar { get; } = DOUBLE_QUOTE;

            public int[] ColumnLengths { get; } = null;

            public bool IsFixedLengthTabular => ColumnLengths?.Length > 0;

            public TabularRecordEncoding()
            {
            }

            public TabularRecordEncoding(char delimiterChar, char qualifierChar, char escapeChar)
            {
                DelimiterChar = delimiterChar;
                QualifierChar = qualifierChar;
                EscapeChar = escapeChar;
                ColumnLengths = null;
            }

            public TabularRecordEncoding(int[] columnLengths)
            {
                ColumnLengths = columnLengths;
            }

            public TabularRecord ParseRecord(char[] csvRow)
            {
                List<string> result = new List<string>();
                if (IsFixedLengthTabular)
                {
                    //parse the csv chars as fixed-length columns
                    int columnStartIndex = 0;
                    for (int i = 0; i < ColumnLengths.Length; i++)
                    {
                        string rowRemainingSubstring = null;
                        if (columnStartIndex < csvRow.Length)
                        {
                            rowRemainingSubstring = new string(csvRow, columnStartIndex, csvRow.Length - columnStartIndex);
                        }

                        result.Add(FixLength(rowRemainingSubstring, ColumnLengths[i]));

                        columnStartIndex += ColumnLengths[i];
                    }
                }
                else
                {
                    //
                    //qualifier-char (eg "") is the only way of including column-demiliter and EOL chars
                    // in a cell as part of the data
                    //NB, we don't remove escape-char and qualifier-char during parsing, we keep it as cell value - keep it simple.
                    /*

                         tabular line examples - 

                         cell1,cell2,cell3,cell4\n  =>  cell1 | cell2 | cell3 | cell4
                         cell1,cell2,cell3,cell4\r

                         -- for dividing CSV columns, we solely rely on column-demiliter and EOL chars (see bove)
                         -- qualifier (if configured, eg ") and escape-char (eg \) are used for including column-demiliter and EOL chars
                            as part of column data
                         -- NB, we don't un-escape/unqualify column value when storing them, we keep them as they are in source.

                         -- open-end qualifier chars are only valid if they are at the begining/end of a cell.
                         -- DANGER! a openning qualifier without a corresponding closing qualifier will disgards the rest of the data
                         cell1,cell2,"cell,3",cell4\n  =>  cell1 | cell2 | "cell,3" | cell4
                         cell1,ce"ll2,"cell,3",cell4\n  =>  cell1 | ce"ll2 | "cell,3" | cell4
                         cell1,cell2,"cell\r3",cell4\n  =>  cell1 | cell2 | "cell\r3" | cell4

                        -- escape-char 'disables' its following delimiter/CRLF char, making the delimiter/CRLF as part of the cell
                         cell1\,cell2,"cell\r3",cell4\n  =>  cell1,cell2 | "cell\r3" | cell4
                         \"cell1\",cell2\,,"cell\r3",cell4\n  =>  "cell1" | cell2, | cell\r3 | cell4
                         \"cell1\",cell2,cell3\\",cell4\n  =>  \"cell1\" | cell2 | cell\\" | cell4


                         */


                    //parse the columns using DelimiterChar and QualifierChar
                    //for each char read from the input buffer, do - 
                    //1 - if it's a special char (and prev_c), exam the state and decide .. 
                    //if a)pack the cell-buffer, b) discard the buffer, c) discard the char d) keep the char (as usual)
                    // and if need to chage state
                    bool qualifyingStateON = false;
                    bool escapingStateON = false;
                    char _prev_c = '\0', c = _prev_c;
                    StringBuilder cellBuffer = new StringBuilder();
                    for (int pos = 0; pos < csvRow.Length; pos++)
                    {
                        c = csvRow[pos];

                        /*
                         The logic is like this -
                         Assumption 1: qualifying only can start at the begining of a cell (cell-buffer is empty), and must end by a closing qualifier followed by a 
                         delimiter or the end of the line char[]. In qualifying-mode, all encoding chars loses their meaning, except the escaping char (see next assumption).

                         Assumption 2: escaping can happen only within a cell (cell-buffer is not empty), in which case an escape-char "disables" the encoding meaning 
                         of the qualifier/escape char (not delimiter) if there is one that follows it (i.e. the following char is kept as a part of the cell's value, and 
                         the escape char is discarded); if there is no encoding char folows the escape char, then both the escape char and the following char are 
                         kept as the cell's value.

                         */
                        if (!qualifyingStateON)
                        {
                            if (c == QualifierChar && cellBuffer.Length == 0)
                            {
                                qualifyingStateON = true;
                                continue;   //scan next char
                            }
                            else if (c == DelimiterChar || pos == csvRow.Length - 1)
                            {
                                //pack the cell if delimiter/eol is found
                                result.Add(cellBuffer.ToString());
                                cellBuffer.Clear();
                            }
                            else
                            {
                                ///build the cell (data) buffer
                                if (!escapingStateON)
                                {
                                    if (c == EscapeChar && cellBuffer.Length > 0)
                                    {
                                        escapingStateON = true;
                                    }

                                    cellBuffer.Append(c);
                                }
                                else
                                {
                                    /* escaping mode */
                                    if (c == EscapeChar || c == QualifierChar)
                                    {
                                        cellBuffer.Remove(cellBuffer.Length - 1, 1);
                                    }

                                    cellBuffer.Append(c);

                                    //turn off escaping mode regardlessly after scanning the "next char"
                                    escapingStateON = false;
                                }
                            }
                        }
                        else
                        {
                            /* qualifying mode - delimiters are kept as cell value */
                            if (!escapingStateON)
                            {
                                //looking for ending-qualifier + end of cell (delimiter or EOL)
                                if (c == QualifierChar && (pos == csvRow.Length - 1 /*EOL*/ || csvRow[pos + 1] == DelimiterChar))
                                {
                                    //pack the cell
                                    result.Add(cellBuffer.ToString());
                                    cellBuffer.Clear();
                                    pos++;  //skips the delimiter-char, or break the for-loop
                                }
                                else
                                {
                                    cellBuffer.Append(c);

                                    if (c == EscapeChar)
                                    {
                                        escapingStateON = true;
                                    }
                                }
                            }
                            else
                            {
                                /* escaping mode */

                                //in escaping mode, it has special treatment only to these two chars
                                // that is, to keep them as actual data value, not encoding chars
                                if (c == EscapeChar || c == QualifierChar)
                                {
                                    //firstly, remove the previously added escape char
                                    cellBuffer.Remove(cellBuffer.Length - 1, 1);
                                }
                                //else the previously added escape char is kept as value

                                cellBuffer.Append(c); //any char is kept in escaping mode

                                //turn off escaping mode regardlessly after scanning the "next char"
                                escapingStateON = false;
                            }
                        }

                    }  // <-- buffer scanning finished.

                    //pack the last cell outside the scanning loop
                    if (cellBuffer.Length > 0)
                    {
                        result.Add(cellBuffer.ToString()); //pack the last cell's data (not trigger by delimiter, but by end-of-buffer)
                    }
                }

                return new TabularRecord(result);
            }

            public virtual IRda Parse(char[] csvRow)
            {
                return ParseRecord(csvRow);
            }

            internal string Encode(List<string> itemValues)
            {
                StringBuilder result = new StringBuilder();
                if (IsFixedLengthTabular)
                {
                    //encode the csv chars as fixed-length columns
                    for (int i = 0; i < ColumnLengths.Length; i++)
                    {
                        string value = i < itemValues.Count ? itemValues[i] : null;
                        result.Append(FixLength(value, ColumnLengths[i]));
                    }
                }
                else
                {
                    //encode the columns using DelimiterChar and QualifierChar
                    foreach (string column in itemValues)
                    {
                        result.Append(DelimiterChar).Append(Qualify(column));
                    }

                    if (result.Length > 0)
                    {
                        result.Remove(0, 1);
                    }
                }

                return result.ToString();
            }

            public string Qualify(string pValue)
            {
                if (pValue == null)
                {
                    return string.Empty;
                }
                else
                {
                    if (pValue.Contains(QualifierChar.ToString()))
                    {
                        pValue = pValue.Replace(QualifierChar.ToString(), $"{EscapeChar}{QualifierChar}");
                    }
                    if (pValue.Contains(DelimiterChar.ToString()) ||
                        pValue.Contains("\n") || pValue.Contains("\r") ||
                        pValue.Contains(EscapeChar.ToString()) ||
                        pValue.Contains(QualifierChar.ToString()))
                    {
                        return $"{QualifierChar}{pValue}{QualifierChar}";
                    }
                }
                return pValue;
            }

            private string FixLength(string source, int length)
            {
                if (length < 0) { throw new Exception($"Invalid column length {length}."); }

                if (string.IsNullOrEmpty(source))
                {
                    return new string(' ', length);
                }
                else
                {
                    return source.Length >= length ? source.Substring(0, length) : source.PadRight(length);
                }
            }

            public char[] Encode(IRda record)
            {
                if (record is TabularRecord csvRow)
                {
                    return Encode(csvRow.ItemValues).ToCharArray();
                }
                else
                {
                    return null;
                }
            }

            public Encoding TextEncoding { get; set; } = Encoding.Default;
        }

        public class TabularRecordStreamScanner : AbstractCharStreamRecordScanner
        {

            private char csvColumnDelimiter => RecordEncoding.DelimiterChar;
            private char csvColumnQualifier => RecordEncoding.QualifierChar;

            private bool convertInCellLfCrToSpace = false;
            private StringBuilder LastRoundScannedChars { get; set; } = new StringBuilder();

            //all the chars since the last cell is kept here
            //protected StringBuilder LineReadBuffer { get; set; } = new StringBuilder();
            //protected StringBuilder CellReadBuffer { get; set; } = new StringBuilder();

            /******               DETECTING NEW LINE 
             look for both CR and LF, the first encounted CR or LF will be stored for future line-break detection, except -
             - if the leading lines are blank, or 
             - if the first (non-blank) line doesn't match the specified column count - in this case, keeps looking for CR/LF

             If the stored line-break char failed to be used for further parsing, eg, doesn't match the specified column count, report it as error
             - TODO in the furture, this can be enhanced (eg adaptive) if the sub-sequent line are consistant
            */
            //line break can be \r, \n, or \r\n
            const char CR = '\r';
            const char LF = '\n';

            public TabularRecordStreamScanner(ILoggingProvider logger, TabularRecord.TabularRecordEncoding recordEncoding) : base(logger)
            {
                //check nullness and set default values
                RecordEncoding = recordEncoding;
                this.convertInCellLfCrToSpace = true;
            }

            TabularRecord.TabularRecordEncoding RecordEncoding { get; }

            enum QUALIFYING_STATE
            {
                ON,     /* start ignoring matching line-breaks */
                OFF
            };

            //// initial state. 
            //// These states are maintained across multiple buffer reads
            QUALIFYING_STATE _qualifyingState = QUALIFYING_STATE.OFF;
            char _prev_c = TabularRecord.TabularRecordEncoding.NULL_CHAR; //signal we've just started a new line


            //in building CSV cells, we solely rely on column-demiliter and EOL chars
            //escape-char (eg \) and qualifier-char (eg "") are the two ways of including column-demiliter and EOL chars
            // in a cell as part of the data
            //NB, we don't remove escape-char and qualifier-char, we keep it as cell value - keep it simple.
            protected override async Task ScanStreamReadBufferAsync(char[] inputReadBuffer, int availableDataLengthInBuffer)
            {

                //for each char read from the input buffer, do - 
                //1 - if it's a special char (and prev_c), exam the state and decide .. 
                //if a)pack the cell-buffer, b) discard the buffer, c) discard the char d) keep the char (as usual)
                // and if need to chage state
                char c = _prev_c;
                for (int pos = 0; pos < availableDataLengthInBuffer; pos++)
                {
                    _prev_c = c;
                    c = inputReadBuffer[pos];

                    /*
                     The logic is like this -
                     1) when state == "qualifying-on", we'll only care when to turn "qualifying-off", 
                        and that's only when we encounter the next qualifier.
                     2) (only) when state == "qualifying-off", we can switch between sub-state "detect-linebreak" on/off
                        2a) it becomes "on" when we encounter the begining of the linebreak chars
                        2b) it becomes "off" when i) the linebreak sequences chars are all matched - that's when we "pack a line" (and drop the linebreak chars)
                        , or ii) the sequence-matching is interrupted, the "supposed linebreak" sequence chars being read becomes line data, and in such case 
                        we'll also watch for the qualifier-char, in which case we might be entering into the "qualifiying-on" state.

                     */
                    if (_qualifyingState == QUALIFYING_STATE.ON)
                    {
                        /* we're in "qualifying-on" state, we don't handle line-break nor cell-delimitoring.
                         * we'll only care when to "turn qualifying-off" */

                        //qualifying is turned off only when qualifier-char is followed by a CR/CF, or column-delimitor
                        if (_prev_c == csvColumnQualifier)
                        {
                            if (c == csvColumnDelimiter)
                            {
                                LastRoundScannedChars.Append(csvColumnDelimiter);
                                _qualifyingState = QUALIFYING_STATE.OFF;
                            }
                            else if (c == CR || c == LF)
                            {
                                //ValidateCleansAndSave() clears buffer also sets _qualifyingState = QUALIFYING_STATE.OFF
                                await HarvestBufferedRecords();
                            }
                            else
                            {
                                LastRoundScannedChars.Append(c);
                            }
                        }
                        else
                        {
                            //pack the char as is, unless it's a control-char - which may be cleansed as a ' '
                            if (Char.IsControl(c) && c != '\t')
                            {
                                if (convertInCellLfCrToSpace == true) { LastRoundScannedChars.Append(' '); }
                            }
                            else
                            {
                                LastRoundScannedChars.Append(c);
                            }
                        }
                    }
                    else
                    {
                        /* we're in QUALIFYING_STATE.OFF state, we'll look for line-break chars for "pack a line", 
                         * if current char is none of these special char, we'll just buffer it. 
                         */

                        if (c == CR || c == LF)
                        {
                            //we're not in qualifying state, and we found a line-break char
                            //we ... harvest the line!   (NB, line break can be \r, \n, or \r\n)
                            if (LastRoundScannedChars.Length > 0) //lineReadBuffer will be empty if we encounted \r\n\r\n etc
                            {
                                //NB2, we don't deal with column parsing- which is done in HarvestRecordInBuffer()
                                await HarvestBufferedRecords(); //clears buffer also sets _qualifyingState = QUALIFYING_STATE.OFF
                            }

                            //else, don't save this blank line
                            //in effect, we throw away (consective) line-break chars, 
                            //consective line-breaks \r\n would have empty LineReadBuffer
                        }
                        else if (c == csvColumnDelimiter)
                        {
                            //replace it to internal separator char
                            LastRoundScannedChars.Append(csvColumnDelimiter);
                        }
                        else
                        {
                            //pack the char as is, unless it's a control-char - which may be cleansed as a ' '
                            if (Char.IsControl(c) && c != '\t')
                            {
                                if (convertInCellLfCrToSpace == true) { LastRoundScannedChars.Append(' '); }
                            }
                            else
                            {
                                LastRoundScannedChars.Append(c);
                            }

                            //check if we need start qualifying state, (note we restrict qualifying-state can only start at the begining of a cell)
                            if (c == csvColumnQualifier && (_prev_c == NULL_CHAR || _prev_c == csvColumnDelimiter))
                            {
                                _qualifyingState = QUALIFYING_STATE.ON;
                            }
                        }
                    }
                }  // <-- buffer scanning finished.
            }

            public static readonly char NULL_CHAR = '\0';

            protected override async Task HarvestBufferedRecords()
            {
                string line1 = LastRoundScannedChars.ToString();
                if (line1.Trim().Length == 0) { return; }    // skip blank lines 

                //TODO implement the "enforce column count consistency" feature

                //record is validated and cleansed, now save 
                await AddToCollectionAsync(line1.ToCharArray());

                LastRoundScannedChars.Clear();
                _qualifyingState = QUALIFYING_STATE.OFF;
            }

            // a simplified, synchronised warpper version of CSV-record scanning
            // returns csv records from scanning the input.
            public static List<char[]> Scan(char[] input, TabularRecord.TabularRecordEncoding recordEncoding)
            {
                //Stream stream = new MemoryStream(recordEncoding.TextEncoding.GetBytes(input));
                TabularRecordStreamScanner scanner = new TabularRecordStreamScanner(null, recordEncoding);
                scanner.ScanRecordsInStreamAsync(input, recordEncoding.TextEncoding, new System.Threading.CancellationToken()).Wait();
                List<char[]> result = new List<char[]>();
                while (scanner.HarvestedRecords.TryTake(out var record))
                {
                    result.Add(record);
                }
                return result;
            }

            public override IRda Parse(char[] csvRecordChars, Encoding default1)
            {
                return RecordEncoding.ParseRecord(csvRecordChars);
            }
        }


    }
}