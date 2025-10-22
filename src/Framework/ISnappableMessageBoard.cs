using Charian;
using System;
using System.Collections.Generic;

namespace Foldda.Automation.Framework
{
    /// <summary>
    /// Message Board is maintained by runtime for async comminications between runtime parties eg handler-hanlder, or handler-runtime. 
    /// 
    /// Each entry on the board contains -
    /// 
    /// 1) Sender & Receiver refs -- eg handler-id, sender to fill these indication where the message is from and to
    /// 2) Message - an IRda object eg it can be a return value of a function call
    /// 3) Post time - the time the message is posted
    /// 4) Expiry time - runtime will clear the entry once it's expired.
    /// 
    /// Hint, for collabrative/syncronised work, a receiver can use the message board to reply an "acknowledgement" message, and 
    /// a sender can process to its next step upon the acknowledgement
    /// 
    /// Only runtime can collect messages and re-distribute the message, as payload of a "command", to the intended recipient(s) 
    /// 
    /// Security, ideally, only sender/receiver can update a board entry; only runtime can post broadcast messages (ie. targeting multiple receivers)
    /// 
    /// </summary>
    public interface ISnappableMessageBoard
    {
        /*
            An example of a message-board entry ("a message") that would be maintained by a runtime - 

            class MessageBoardEntry : IRda
            {
                public string SenderId;
                public string ReceiverId;
                public IRda Message;
                public DateTime PostTime;
                public DateTime ExpiryTime;
            }
 
         */

        /// <summary>
        /// This calls the runtime to construct a "message board post entry" from the provided parameters and maintain it internally
        /// </summary>
        /// <param name="sender">The message-sending handler</param>
        /// <param name="receiverId">The intended receiver (handler or runtime) id</param>
        /// <param name="message">the message</param>
        /// <param name="expiryTime">the time this message expires and deleted from the board, by runtime</param>
        void PostMessage(ISnappableManager sender, string receiverId, IRda message, DateTime expiryTime);

        /// <summary>
        /// Collects messages addressed to a specified receiver from this board, optionally specifying to receive non-addressed, broadcasted messages.
        /// </summary>
        /// <param name="receiver">The caller/message-receiver</param>
        /// <returns></returns>
        List<IRda> CollectMessage(ISnappableManager receiver);
    }


}