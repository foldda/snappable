using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Foldda.Automation.Util
{
    /*
     * This class looks for a header patternt in the char stream, AppendAndMatch() return the matched header char[]
     * if pattern is found, then the client has to decide what to do with the chars in the buffer, which can be 
     * 1) get all chars as array, do cleansing etc => buffer.ToArray(), and/or 
     * 2) put "unused/remaining/re-usable" chars back to the buffer => buffer.Reset(chars[])
     * 3) resume further scanning
     * 
     */
    public class CharsArrayRecordMatcher
    {
        public char[] RecordMarkerChars { get; private set; }
        private StringBuilder Buffer { get; } = new StringBuilder();

        int matchedIndex = 0;

        public CharsArrayRecordMatcher(char[] header)
        {
            RecordMarkerChars = header;
        }

        //append the char to buffer, and match the marker pattern in the buffer
        //if matched pattern is found, return the data before the marker as "the record"
        //if pattern not found, return null;
        public char[] AppendAndFetch(char c)
        {
            char[] result = null;
            char[] matchedPattern = AppendAndMatch(c);
            if(matchedPattern != null && Buffer.Length > matchedPattern.Length)
            {
                result = Buffer.ToString(0, Buffer.Length - matchedPattern.Length).ToCharArray();
                Buffer.Clear().Append(matchedPattern);
            }
            return result;
        }

        //return as char[] from the data in buffer;
        public char[] FetchCurrentlyBufferred()
        {
            return Buffer.ToString().ToCharArray();
        }


        //return as char[] from the data in buffer;
        public void ClearCurrentlyBufferred()
        {
            Buffer.Clear();
        }

        //returns the matched header[] if it's found after stuffing the char
        //the caller then have the option to get the bufferred chars[], or 
        //(null if no full-match yet)
        protected char[] AppendAndMatch(char c)
        {
            Buffer.Append(c);
            if (c == RecordMarkerChars[matchedIndex])
            {
                matchedIndex++;
                if (matchedIndex == RecordMarkerChars.Length)
                {
                    matchedIndex = 0;
                    return RecordMarkerChars;
                }
                else
                {
                    return null;   //not full match (yet)
                }
            }
            else
            {
                matchedIndex = 0;
                return null;   //mis-match, reset index and restart
            }
        }
    }
}
