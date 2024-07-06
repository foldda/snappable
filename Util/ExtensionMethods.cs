using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Foldda.Automation.Util
{

    public static class ExtensionMethods
    {
        public static bool Between(this DateTime dt, DateTime start, DateTime end)
        {
            if (start < end) return dt >= start && dt <= end;
            return dt >= end && dt <= start;
        }

        public static bool IsQuoted(this string str)
        {
            if (str == null) { return false; }
            return str.Length >= 2 && (str.IndexOf('"') == 0) && (str.LastIndexOf('"') == str.Length - 1);
        }

        //strip the first char and the last char
        //intended for being used for unquote a quoted string
        public static string StripEnds(this string str)
        {
            if (string.IsNullOrEmpty(str) || str.Length == 1) { return str; }
            return str.Substring(1, str.Length - 2);
        }

        //intended for quoting a string
        public static string AddQuotes(this string str)
        {
            return $"\"{str}\"";
        }

        public static string StripControlChars(this string str)
        {
            return new string(str.Where(c => !char.IsControl(c)).ToArray());
        }

        public static string[] SplitAndTrim(this string str, char splitChar)
        {
            return str?.Split(splitChar).Select(p => p.Trim()).ToArray();
        }

        //generate a string made with random chars with the given length
        public static string Random(this string seedString, int length)
        {
            var chars = seedString+"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());
        }

        public static string GetFullMessage(this Exception ex)
        {
            return ex.InnerException == null
                    ? ex.Message
                    : ex.Message + " --> " + ex.InnerException.GetFullMessage();
        }

        //returns items "currently exists" in a blocked collection.
        //note if the collection changed during this method, the items returned may not be accurate or the intended
        //but at least it won't cause exception, and the next run (when itmes not changing) will correct the result.
        public static List<T> Snap<T>(this BlockingCollection<T> @this, bool removeSnapped)
        {
            if (@this == null) { return null; }
            List<T> result = new List<T>();
            int count = @this.Count;    //count prevents it becomes an infinite-loop
            while (count > 0 && @this.TryTake(out T item))
            {
                result.Add(item);
                if (!removeSnapped) { @this.TryAdd(item); }
                count--;
            }

            return result;
        }

        public static void AddRange<T>(this BlockingCollection<T> @this, ICollection<T> range)
        {
            if (@this == null)
            {
                throw new ArgumentNullException("blockingCollection");
            }            
            
            foreach(var item in range)
            {
                @this.Add(item); 
            }
        }

        public static void Clear<T>(this BlockingCollection<T> blockingCollection)
        {
            if (blockingCollection == null)
            {
                throw new ArgumentNullException("blockingCollection");
            }

            while (blockingCollection.TryTake(out _)) { }
        }

        //returns items "currently exists" in a blocked collection.
        public static void Remove<T>(this BlockingCollection<T> self, object toBeRemoved)
        {
            lock (self)
            {
                if (self == null) { return; }
                List<T> result = new List<T>();
                int count = self.Count;   //count prevents it becomes an infinite-loop
                while (count > 0 && self.TryTake(out T item))
                {
                    result.Add(item);
                    if (toBeRemoved != (item as object)) { self.TryAdd(item); }
                    count--;
                }
            }
        }
    }
}
