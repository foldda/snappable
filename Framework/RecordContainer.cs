using Charian;
using System.Collections.Generic;
using System.Text;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// 
    /// RecordContainer class represents a very loosely-defined data-exchange contract, where MetaData, Records, 
    /// and Processing-context will need to be casted into concrete objects within the business/programming context.
    /// 
    /// For example, when a CSV file is stored inside a DataContainer, it's header columns can be placed in the MetaData section
    /// the data rows each will be encaptured into an Rda object and be placed as the Records collection, and ProcessingContext 
    /// can be connection and login information of the destination database table, and/or data encryption information.
    /// 
    /// A container handler exams a container by attempt to cast the container content to one of its 'known data types',
    /// and the success/failure of such casting dynamically determines the logic the hanlder would use to handle the container.
    /// 
    /// RDA container allows programming-late-binding like behavior, but even further i.e. also can be loose and adaptive, in messaging/data-exchange.
    /// 
    /// </summary>
    public class RecordContainer : IRda
    {
        public enum RDA_INDEX : int { MetaData, Records, ProcessingContext }

        /// <summary>
        /// The meta-data applicable to all records in this container
        /// </summary>
        public IRda MetaData{ get; set; }

        /// <summary>
        /// The 'native' string-encoding for the container's records 
        /// </summary>
        public IContainerRecordEncoding RecordEncoding { get; set; } = new RdaRecordEncoding();


        //https://stackoverflow.com/questions/8695118/what-are-the-file-group-record-unit-separator-control-characters-and-their-usage
        public const char ASCII_RS = (char)30;

        public class RdaRecordEncoding : IContainerRecordEncoding
        {
            public Encoding TextEncoding => System.Text.Encoding.Default;

            public char[] RecordSeparator { get; set; } = new char[] { ASCII_RS };

            public char[] Encode(IRda record)
            {
                return record.ToRda().ToString().ToCharArray();
            }

            public char[] EncodeContainerHeader(Rda containerMetaData)
            {
                return null;
            }

            public char[] EncodeContainerTrailer(Rda containerMetaData)
            {
                return null;
            }

            public IRda Parse(char[] charArray)
            {
                return Rda.Parse(new string(charArray));
            }
        }


        /// <summary>
        /// Container payload
        /// </summary>
        public List<IRda> Records { get; } = new List<IRda>();
        

        /// <summary>
        /// Framework processing related 'context', eg processing instruction
        /// </summary>
        public IRda ProcessingContext { get; set; }

        public void Add(IRda data)
        {
            Records.Add(data);
        }

        public Rda ToRda()
        {
            Rda result = new Rda();
            result[(int)RDA_INDEX.MetaData] = MetaData.ToRda();
            foreach(var record in Records)
            {
                result[(int)RDA_INDEX.Records].Elements.Add(record.ToRda());
            }
            result[(int)RDA_INDEX.ProcessingContext] = ProcessingContext.ToRda();

            return result;
        }

        public virtual IRda FromRda(Rda rda)
        {
            //sub-class to implement desrialization here
            //.... (late binding) restoring MetaData, Records, and ProcessingContext

            return this;
        }
    }
}
