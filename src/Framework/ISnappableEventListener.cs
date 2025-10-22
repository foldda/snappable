using System.Threading;

namespace Foldda.Automation.Framework
{
    public interface ISnappableEventListener
    {
        void ProcessEvent(MessageRda.HandlerEvent evnt, CancellationToken cancellationToken);
    }
}