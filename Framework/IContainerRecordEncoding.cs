using Charian;

namespace Foldda.Automation.Framework
{
    public interface IContainerRecordEncoding
    {
        /// <summary>
        /// Used for converting the encoded text record to char[] and vise versa
        /// </summary>
        System.Text.Encoding TextEncoding { get; }

        /// <summary>
        /// Once the boundary of the record is determined within the char stream scanning, this method does the parsing
        /// the chars and the construction of the actual record which must implement the IRda interface
        /// </summary>
        /// <param name="charArray">the char[] (boundary) that contains the record</param>
        /// <returns>The record</returns>
        IRda Parse(char[] charArray);
        
        /// <summary>
        /// Encode the Rda record to the specific type-of-record's encoding standard, eg HL7 or delimited CSV
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        char[] Encode(IRda record);
    }
}