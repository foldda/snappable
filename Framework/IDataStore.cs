using Charian;
using System.Collections.Generic;

namespace Foldda.Automation.Framework
{
    public interface IDataStore
    {
        //used in container info
        string Id { get; }

        List<IRda> CollectReceived();

        //both DataContainer and HandlerEvent are IRda objects, 
        void Receive(IRda item);

        ////receives a container (of records) and adds the content to the Received collection
        ////possibly applying data-processing such as filtering based on the container's label metadata
        //void Receive(RecordContainer container);

        ////receives a record and adds to a container (store-created, where dataSenderId is a required meta-data) in the Received collection
        //void Receive(IRda record, string dataSenderId);  
        
        ////receives a event
        //void Receive(HandlerEvent event1);
    }
}