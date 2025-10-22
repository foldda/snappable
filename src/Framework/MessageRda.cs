using Charian;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Foldda.Automation.Framework
{
    //
    /// <summary>
    /// FrameworkMessage is a generic type of data record that is passed between handlers. It has a time component
    /// and a handler-dependent "details".
    /// 
    /// A typical pattern is, a hanlder, along with its data-processing logic, can also (optionally) define 
    /// input-context Rda, and/or an output-context Rda, and these Rda are embeded inside FrameworkMessage to 
    /// trigger/respond colaborative events between handlers. I.e., connected hanlder 'understand' each other's
    /// trigger event context, and be able to respond accordingly.
    /// 
    /// </summary>
    public class MessageRda : Rda
    {
        enum SEGMENTS : int { MESSAGE_HEADER, MESSAGE_BODY, MESSAGE_TRAILER }

        //Mete-data inclduing message-type, identifiers/UID, sender info and receiver address, delivery/routing instructions
        protected Rda MessageHeaderRda => this[(int)SEGMENTS.MESSAGE_HEADER];

        //data that is only meaningful to sender and receiver
        protected Rda MessageBodyRda => this[(int)SEGMENTS.MESSAGE_BODY];

        //(optional) used by framework for maintaining integraty - signature, checksum, expiry, encoding/decoding
        protected Rda MessageTrailerRda => this[(int)SEGMENTS.MESSAGE_TRAILER];

        private MessageRda() { }



        public class HandlerEvent : MessageRda
        {
            enum HANDLER_EVENT : int { EVENT_SOURCE_ID, EVENT_TIME_TOKENS }

            public HandlerEvent(string sourceId, DateTime time, IRda eventDetails)
            {
                EventSourceId = sourceId;
                EventTime = time;
                EventDetailsRda = eventDetails == null ? Rda.NULL: eventDetails.ToRda();
            }

            public string EventSourceId
            {
                get => MessageHeaderRda[(int)HANDLER_EVENT.EVENT_SOURCE_ID].ScalarValue;
                set => MessageHeaderRda[(int)HANDLER_EVENT.EVENT_SOURCE_ID].ScalarValue = value;
            }

            public DateTime EventTime 
            { 
                get
                {
                    return MakeDateTime(MessageHeaderRda[(int)HANDLER_EVENT.EVENT_TIME_TOKENS].ChildrenValueArray);
                }
                set
                {
                    MessageHeaderRda[(int)HANDLER_EVENT.EVENT_TIME_TOKENS].ChildrenValueArray = MakeDateTimeTokens(value);
                }
            }

            public Rda EventDetailsRda
            {
                get => MessageBodyRda;
                set => MessageBodyRda.FromRda(value ?? Rda.NULL);
            }

            public override string ToString()
            {
                return $"{EventSourceId} - {EventTime}";
            }
        }

        public class HandlerNotification : MessageRda
        {
            enum HANDLER_NOTIFICATION : int { SENDER_ID, RECEIVER_ID, TYPE }
            enum NOTIFICATION_TYPE : int { UNKNOWN = -1, ENTITY_COMMAND = 0, ENTITY_CURRENT_STATE = 1, E = 2, RESERVED_TYPE_2 = 2, RESERVED_TYPE_3 = 3, RESERVED_TYPE_4 = 4 }

            public HandlerNotification(string senderId, string receiverId, IRda notificationBody)
            {
                SenderId = senderId;
                ReceiverId = receiverId;
                NotificationBodyRda = notificationBody.ToRda();
                ExpiryDateTime = DateTime.Now.AddSeconds(1);    //default, which can be specifically set after creation.
            }

            public bool IsBroadcast => string.IsNullOrEmpty(ReceiverId) && ExpiryDateTime > DateTime.UtcNow;

            public DateTime ExpiryDateTime { get; set; }

            public string SenderId
            {
                get => MessageHeaderRda[(int)HANDLER_NOTIFICATION.SENDER_ID].ScalarValue;
                set => MessageHeaderRda[(int)HANDLER_NOTIFICATION.SENDER_ID].ScalarValue = value;
            }

            public string ReceiverId
            {
                get => MessageHeaderRda[(int)HANDLER_NOTIFICATION.RECEIVER_ID].ScalarValue;
                set => MessageHeaderRda[(int)HANDLER_NOTIFICATION.RECEIVER_ID].ScalarValue = value;
            }

            public int Type
            {
                get => Int32.TryParse(MessageHeaderRda[(int)HANDLER_NOTIFICATION.TYPE].ScalarValue, out int result) ? result : -1;
                set => MessageHeaderRda[(int)HANDLER_NOTIFICATION.TYPE].ScalarValue = value.ToString();
            }


            public Rda NotificationBodyRda
            {
                get => MessageBodyRda;
                set => MessageBodyRda.FromRda(value);
            }

            public override string ToString()
            {
                return $"Notification ({SenderId} -> {ReceiverId})";
            }

        }


        interface IHeader : IRda
        {
            string Originator { get; }
        }


        public MessageRda(Rda rda)
        {
            FromRda(rda);
        }

    }
}