using Charian;
using System.Collections.Generic;


namespace Foldda.DataAutomation.Framework
{
    /// <summary>
    /// 
    /// DataContainer class represents a very loosely-defined data-exchange contract, where MetaData, Records, 
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
    public class DataContainer : Rda
    {
        private enum RDA_INDEX : int { MetaData, Records, ProcessingContext }

        /// <summary>
        /// The meta-data applicable to all records in this container
        /// </summary>
        public Rda MetaData
        {
            get => this[(int)RDA_INDEX.MetaData];
            set => this[(int)RDA_INDEX.MetaData] = value;
        }

        /// <summary>
        /// Container payload
        /// </summary>
        public List<Rda> Records => this[(int)RDA_INDEX.Records].Elements;

        /// <summary>
        /// Framework processing related 'context', eg processing instruction
        /// </summary>
        public Rda ProcessingContext
        {
            get => this[(int)RDA_INDEX.ProcessingContext];
            set => this[(int)RDA_INDEX.ProcessingContext] = value;
        }

        public void Add(Rda data)
        {
            Records.Add(data);
        }
    }
}
