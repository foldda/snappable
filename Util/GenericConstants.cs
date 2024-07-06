using Foldda.Automation.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Foldda.Automation.Util
{
    /// <summary>
    /// Agreement is for storing Foldda-specific, cross-project common constants.
    /// 
    /// Note the Version constant used for validation the correct version of the Foldda Common package are used 
    /// between projects.
    /// 
    /// </summary>
    public static class GenericConstans
    {
        public const int VERSION_MAJOR = 1;     //incompatible container structural change
        public const int VERSION_MINOR = 0;     //compatible container structural change, eg adding new header encoder, 
        public const int REVISION = 0;          //section definition or constant name-change or adding new section-def
        public const int PATCH = 0;             //implementation change or bug-fix

        public static int[] Version { get; } = new int[] { VERSION_MAJOR, VERSION_MINOR, REVISION, PATCH };

        public static readonly char ASCII_FS_CHAR = (char)0x1c; /* ASCII File-separator */
        public static readonly char ASCII_GS_CHAR = (char)0x1d; /* ASCII Group-separator */
        public static readonly char ASCII_RS_CHAR = (char)0x1e; /* ASCII Record-separator */
        public static readonly char ASCII_US_CHAR = (char)0x1f; /* ASCII Unit-separator */

        public static readonly string YES_STRING = "YES";
        public static readonly string NO_STRING = "NO";

        public static readonly char DOUBLE_QUOTE = '"';
        public static readonly char COMMA = ',';
        public static readonly char NULL_CHAR = '\0';

    }

}
