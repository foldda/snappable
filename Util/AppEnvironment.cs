using Foldda.DataAutomation.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.DataAutomation.Util
{
    public static class AppEnvironment
    {
        //https://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}

