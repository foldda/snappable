using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Foldda.Automation.Framework;
using System.Text;
using Charian;
using Foldda.Automation.Util;
using System.Threading;
using System.Data;
using System.Linq;
using System.Collections.Concurrent;

namespace Foldda.Automation.HL7Handler
{

    public abstract class BaseHL7Handler : BasicSnappable
    {
        public BaseHL7Handler(ISnappableManager manager) : base(manager) { }

        //protected IRda ProcessContainerRecord(DataContainer.Record record, CancellationToken cancellationToken)
        //{
        //    HL7Message hl7Record = record.Data as HL7Message;
        //    return ProcessHL7Record(hl7Record, record.Container.Label.ToRda(), cancellationToken);
        //}

        protected override AbstractCharStreamRecordScanner GetDefaultFileRecordScanner(ILoggingProvider loggingProvider)
        {
            return new HL7Message.HL7MessageScanner(loggingProvider, HL7Message.HL7MessageEncoding.Default);
        }

        /// <summary>
        /// Process a record inputContainer - passed in by the handler manager.
        /// Note this handler would deposite its output, if any, to a designated storage from the manager
        /// </summary>
        /// <param name="inputContainer">a inputContainer with a collection of records</param>
        /// <returns>a status integer</returns>
        public override Task<int> ProcessPipelineRecordContainer(RecordContainer inputContainer, CancellationToken cancellationToken)
        {
            //HandlerManager.PipelineOutputDataStorage.Receive(inputContainer);    //default is pass-thru

            ///alternatively processing each record indivisually ... something like

            RecordContainer outputContainer = new RecordContainer() 
            { 
                MetaData = new RecordContainer.DefaultMetaData(this.UID, DateTime.UtcNow) 
                { 
                    OriginalMetaData = inputContainer.MetaData //keep the input container's meta-data here
                } 
            };
            foreach (var record in inputContainer.Records)
            {
                if (record is HL7Message hl7Message)
                {
                    ProcessInputHL7MessageRecord(hl7Message, outputContainer, cancellationToken);
                }
                else
                {
                    Log($"WARNING: Record of type {record.GetType().FullName} is ignored. Record => {record?.ToRda()}");
                }
            }

            Manager.PipelineOutputDataStorage.Receive(outputContainer);

            return Task.FromResult(0);
        }

        protected virtual Task ProcessInputHL7MessageRecord(HL7Message record, RecordContainer outputContainer, CancellationToken cancellationToken)
        {
            //default is a pass-through
            outputContainer.Add(record);
            return Task.Delay(50);
        }

        public class MllpConnectionHandler
        {

            public enum State : int { FOR_HEADER_BYTE, FOR_TRAIL_BYTE1, FOR_TRAIL_BYTE2, FAULTY };
            //
            public delegate char[] MllpHanlder(char[] message);


            //finite state  machine for MLLP payload scanning 
            public State HandlingState;
            private StringBuilder Received;
            private BlockingCollection<char[]> ReceivedMllpPayload { get; set; }


            private Stream networkStream;
            private Encoding encoding;
            private string connectionClientId = string.Empty;

            private MllpConnectionHandler(Stream networkStream, Encoding encoding)
            {
                this.networkStream = networkStream;
                this.encoding = encoding;
                this.Received = new StringBuilder();
                this.ReceivedMllpPayload = new BlockingCollection<char[]>(10);
            }

            public MllpConnectionHandler(Stream networkStream, Encoding encoding, string remoteClientId) : this(networkStream, encoding)
            {
                this.connectionClientId = remoteClientId;
            }

            public async Task HandleHl7MessageReceiveAsync(HL7NetReceiver.AckManager ackManager, CancellationToken token, ILoggingProvider loggingClient)
            {
                byte[] buffer = new byte[2048];
                int count = 0;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        //scan pending messages for processing
                        count = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (count == 0)
                        {
                            break;  //task finishes
                                    //throw new Exception("Connection closed, full MLLP packet not found in received data.");
                        }
                        else
                        {
                            this.ScanMllpPayload(buffer, count);

                            while (ReceivedMllpPayload.TryTake(out char[] data) == true)
                            {
                                char[] ack = ackManager.ProcessNetReceived(data, connectionClientId);
                                await SendAsync(ack, loggingClient);
                                loggingClient.Log($"Responsed ACK => '{new string(ack)}'");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    loggingClient.Log(e.ToString());
                }
                finally
                {
                    networkStream.Close();  //CLEAN-UP
                }
            }

            public async Task<char[]> HandleHl7MessageSendAsync(char[] hl7, int timeout, CancellationToken nodeStopCancellationToken, ILoggingProvider loggingClient)
            {
                await SendAsync(hl7, loggingClient);

                //now try to receive for ACK
                byte[] buffer = new byte[2048];
                int count;
                while (true)
                {
                    try
                    {
                        nodeStopCancellationToken.ThrowIfCancellationRequested();
                        var streamReadTask = networkStream.ReadAsync(buffer, 0, buffer.Length);

                        //wait for task to complete with timeout.
                        if (await Task.WhenAny(streamReadTask, Task.Delay(timeout)) == streamReadTask)
                        {
                            // task completed within timeout
                            count = await streamReadTask;
                            if (count == 0)
                            {
                                throw new Exception("Connection closed, full MLLP packet not found in received data.");
                            }
                            this.ScanMllpPayload(buffer, count);

                            if (ReceivedMllpPayload.TryTake(out char[] ack) == true)
                            {
                                //only expect one MLLP payload (the ACK) after sending the message
                                //so finish here
                                return ack;
                            }

                        }
                        else
                        {
                            // timeout logic
                            throw new Exception($"Timeout ({timeout / 1000.0} sec) awaiting ACK.");
                        }
                    }
                    catch (Exception e)
                    {
                        loggingClient.Log(e.ToString());
                        this.HandlingState = State.FAULTY;
                        throw new MllpReceiveException(e.Message);
                    }
                }
            }

            private void ScanMllpPayload(byte[] buffer, int count)
            {
                char[] chars = encoding.GetChars(buffer, 0, count);
                for (int i = 0; i < count; i++)
                {
                    char c = chars[i];
                    if (HandlingState == State.FOR_HEADER_BYTE)
                    {
                        if (c == 0x0b) { HandlingState = State.FOR_TRAIL_BYTE1; }
                    }
                    else if (HandlingState == State.FOR_TRAIL_BYTE1)
                    {
                        if (c == 0x1c) { HandlingState = State.FOR_TRAIL_BYTE2; }
                        //append received byte
                        else { Received.Append(c); }
                    }
                    else if (HandlingState == State.FOR_TRAIL_BYTE2)
                    {
                        if (c == 0x0d)
                        {
                            //save received payload
                            ReceivedMllpPayload.Add(Received.ToString().ToCharArray());
                            //reset buffer and scanning state
                            Received.Clear();
                            HandlingState = State.FOR_HEADER_BYTE;
                        }
                        else
                        {   //restart searching for the 2 trailing chars
                            Received.Append(0x1c).Append(c);    //keep received bytes, 
                            HandlingState = State.FOR_TRAIL_BYTE1;
                        }
                    }
                }
            }

            private async Task SendAsync(char[] hl7, ILoggingProvider loggingClient)
            {
                //wrap HL7 payload in MLLP
                char[] chars = new char[hl7.Length + 3];
                chars[0] = '\v';
                for (int i = 0; i < hl7.Length; i++) { chars[i + 1] = hl7[i]; }
                chars[chars.Length - 2] = (char)0x1c;
                chars[chars.Length - 1] = '\r';

                //send
                try
                {
                    byte[] bytes = encoding.GetBytes(chars);
                    await networkStream.WriteAsync(bytes, 0, bytes.Length);
                    await networkStream.FlushAsync();
                }
                catch (Exception e)
                {
                    loggingClient.Log(e.ToString());
                }
            }
        }

        public class MllpReceiveException : Exception
        {
            public MllpReceiveException()
            {
            }

            public MllpReceiveException(string message) : base(message)
            {
            }

            public MllpReceiveException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

    }
}
