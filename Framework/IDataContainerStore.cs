using Charian;
using System.Collections.Generic;

namespace Foldda.Automation.Framework
{
    public interface IDataContainerStore
    {
        //used in container info
        string DataReceiverId { get; }

        List<DataContainer> CollectReceived();

        //receives a container (of records) and adds the content to the Received collection
        //possibly applying data-processing such as filtering based on the container's label metadata
        void Receive(DataContainer container); 

        //receives a record and adds to a container in the Received collection
        void Receive(IRda record);   
    }
}