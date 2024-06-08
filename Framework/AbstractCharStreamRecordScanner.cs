//using Foldda.OpenConnect.Framework;
using Charian;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.Framework
{
    public abstract class AbstractCharStreamRecordScanner 
    {
        /// <summary>
        /// Saves the scanned records. Note each record is saved as in its "raw form" i.e. a char array, this is to provide maximun compatibility
        /// of this collection.
        /// Also this is a concurrent collection, meaning the scanner can work async to its client, and signaling the scanning completion 
        /// by using this concurrent collection.
        /// </summary>
        public BlockingCollection<char[]> HarvestedRecords { get; internal set; } = new BlockingCollection<char[]>();

        ILoggingProvider Logger { get; }

        public AbstractCharStreamRecordScanner(ILoggingProvider logger)
        {
            Logger = logger;
        }

        protected void Log(string message)
        {
            Logger.Log(message);
        }

        public async Task ScanRecordsInStreamAsync(char[] content, Encoding encoding, CancellationToken cancellationDelegate)
        {
            using (var stream = new MemoryStream(encoding.GetBytes(content)))
            {
                await ScanRecordsInStreamAsync(stream, encoding, cancellationDelegate);
            }
        }

        //public async Task ScanRecordsInStreamAsync(string filePath, Encoding encoding, CancellationToken cancellationDelegate)
        //{
        //    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //    {
        //        await ScanRecordsInStreamAsync(stream, encoding, cancellationDelegate);
        //    }
        //}

        public virtual async Task ScanRecordsInStreamAsync(Stream stream, CancellationToken cancellationCheck)
        {
            await ScanRecordsInStreamAsync(stream, Encoding.Default, cancellationCheck);
        }

        /// <summary>
        /// Continuesly reads a stream and, utilising the abstract ScanStreamReadBuffer() and HarvestRecordInBuffer() methods, it
        /// collects all records in the provided stream data.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="checkCancellation"></param>
        /// <returns></returns>
        public virtual async Task ScanRecordsInStreamAsync(Stream stream, Encoding encoding, CancellationToken cancellationCheck)
        {
            Log($"File-records-scanner started.");
            //await Task.Factory.StartNew(async () =>
            //{
            try
            {
                ////check if this scanner is being reused, if so we need to "renew" the AsyncCollection
                if (HarvestedRecords.IsCompleted == true)
                {
                    HarvestedRecords = new BlockingCollection<char[]>();
                }

                int totalCharsRead = 0;
                Count = 0;
                var scanningStopwatch = System.Diagnostics.Stopwatch.StartNew();
                // the code that you want to measure time comes below

                using (StreamReader streamReader = new StreamReader(stream, encoding))
                {
                    const int inputBufferSize = 8192;
                    char[] streamReadBuffer = new char[inputBufferSize];
                    int actualCharsRead = 0;

                    do
                    {
                        //read available data from stream into the read buffer
                        actualCharsRead = await streamReader.ReadAsync(streamReadBuffer, 0, inputBufferSize);   // --- error reading  file closed

                        //abstract method - the record-scanning.
                        await ScanStreamReadBufferAsync(streamReadBuffer, actualCharsRead);

                        totalCharsRead += actualCharsRead;

                    } while (actualCharsRead > 0 && !cancellationCheck.IsCancellationRequested);

                    await HarvestBufferedRecords();
                }

                //tell the consumer it's done. 
                HarvestedRecords.CompleteAdding();
                scanningStopwatch.Stop();
                Log($"Data-scanning completed: fetched {Count.ToString("#,###")} records from {totalCharsRead.ToString("#,###")} bytes, in {(scanningStopwatch.ElapsedMilliseconds/1000.0).ToString("0.000")} seconds.");
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
            }

            return;
        }


        /// <summary>
        /// When the scanner indentified a "record-start marker" in the data being scanned, it starts pushing the following scaned data into the
        /// "record catching" buffer, and when the scanner thinks the current-record scanning should have finished (it may found the record-ending marker, 
        /// or found the next record's staring-marker, or the record scanning has reach to the end of the stream), it will call this method to pack up the 
        /// record in the record-catching buffer, which may or may not have one record. If a complete record is indentified, it will be saved to the HarvestedRecords
        /// collection.
        /// </summary>
        protected abstract Task HarvestBufferedRecords();

        //parse the chars to a structured record
        public abstract IRda Parse(char[] singleRecordChars, Encoding default1);

        /// <summary>
        /// Identify valid records in the provided stream-read buffer, together with possible remaining data in the "recording catching buffer".
        ///  
        /// Typically implemented as a FSM, in this method, the scanning has a "record-start marker found" state when the data follows 
        /// "record-start" marker is pushed into the recording-catching buffer; the scanning conntinues until 1) another record-start marker found, or 
        /// 2) the stream reading has come to its end, by which time the "build-record" sub-routine is called to "harvest" a record from the "record catching buffer".
        /// </summary>
        /// <param name="streamReadBuffer">the input stream reading buffer (fixed size) that contain data measured by 'charsRead' to be scanned</param>
        /// <param name="charsRead">the number of actual data in the stream-reading-buffer</param>
        /// <param name="recordCatchingBuffer">Caches the possibly valid data for a record, and it's passed between stream reads for scanning continuety.</param>
        /// <returns></returns>
        protected abstract Task ScanStreamReadBufferAsync(char[] streamReadBuffer, int charsRead);


        //output the record to async collection
        protected async Task AddToCollectionAsync(char[] record)
        {
            while (HarvestedRecords.TryAdd(record) == false)
            {
                await Task.Delay(100);
            }
            Count++;
        }

        public int Count { get; private set; }
    }
}