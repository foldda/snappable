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

        public virtual List<IRda> CollectReceived()
        {
            return RdaDataItems.Snap(true); ;
        }

        //container-level buffer
        internal BlockingCollection<IRda> RdaDataItems { get; } = new BlockingCollection<IRda>(200);

        public string Id { get; }

        public void Receive(IRda item)
        {
            RdaDataItems.Add(item);
        }

    }
}
