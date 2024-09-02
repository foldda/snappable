using Charian;
using Foldda.Automation.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Foldda.Automation.Util;

namespace Foldda.Automation.HandlerDevKit
{
    internal class DataStore : IDataStore
    {
        internal DataStore(int storeId)
        {
            Id = storeId.ToString();
        }

        const string format = "yyMMddHHmmss";

        public virtual List<IRda> CollectReceived()
        {
            List<IRda> result = new List<IRda>();
            if (ContainersAndEvents.Count > 0)
            {
                result = ContainersAndEvents.Snap(true);
            }


            RecordContainer temp = new RecordContainer()
            {
                MetaData = new Rda()
                {
                    ScalarValue = $"{Id}-{DateTime.Now.ToString(format)}"
                }
            };

            foreach (var record in LooseRecords.Snap(true))
            {
                temp.Add(record);
            }
            result.Add(temp);

            return result;
        }

        //container-level buffer
        internal BlockingCollection<IRda> ContainersAndEvents { get; } = new BlockingCollection<IRda>(200);

        //record-level buffer "loose records" - to be packed into a store-created container when they are collected.
        internal BlockingCollection<IRda> LooseRecords { get; } = new BlockingCollection<IRda>(200);

        public string Id { get; }

        public void Receive(IRda item)
        {
            ContainersAndEvents.Add(item);
        }

    }
}
