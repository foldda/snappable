using Foldda.Automation.Util;
using Foldda.Automation.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Charian;
using System.Collections.Concurrent;

namespace Foldda.Automation.HandlerDevKit
{
    public class NodeConfig : IConfigProvider
    {
        internal static NodeConfig GetConfig(string configFile)
        {
            NodeConfig config;

            try
            {
                //this will throw exception when home-directory or config file does not exist
                if (!File.Exists(configFile))
                {
                    throw new FileNotFoundException($"Config [{configFile}] not found.");
                }

                //parse the config file - will throw InvalidOperationException if the file is not properly constructed.
                config = (NodeConfig)ConfigParser.Deserialize(typeof(NodeConfig), configFile);
                if (config == null)
                {
                    throw new Exception($"Parsing config file {configFile} unsuccessful.");
                }

                config.ConfigFileFullPath = configFile;

            }
            catch
            {
                throw;
            }

            return config;
        }

        /// <summary>
        /// for handler to get anything (by name) from the context
        /// </summary>
        /// <param name="pname">Name of property.</param>
        /// <returns></returns>
        public IRda GetSetting(string pname)
        {
            return null;
        }

        public int GetSettingValue(string parameterName, int defaultValue)
        {
            int result = defaultValue;
            string intParamNalue = GetFirstParameterVaule(parameterName);
            if (int.TryParse(intParamNalue, out int intValue) == true)
            {
                result = intValue;
            }

            return result;
        }

        public bool GetSettingValue(string parameterName, string valueToMatch, bool defaultResult)
        {
            string paraV = GetFirstParameterVaule(parameterName);
            if (string.IsNullOrEmpty(paraV))
            {
                return defaultResult;
            }
            else
            {
                return valueToMatch.ToUpper().Equals(paraV.ToUpper());
            }
        }

        //get the 1st match parameter
        private string GetFirstParameterVaule(string parameter)
        {
            var res = GetSettingValues(parameter);
            return res.Count > 0 ? res[0] : null;
        }

        //parameter-name is case in-sensitive
        public virtual List<string> GetSettingValues(string parameter)
        {
            /* eg
             * <Parameters>
                <Parameter>
                  <Name>mapping-rule</Name>
                  <Value>PID-5-1=Smith^Johnson^Hall|PID-5-2=Mary^Michelle^May|PID-5-2=Max^Mark^Michael</Value>
                </Parameter>
                ...
                <Parameter>
                  <Name>mapping-rule</Name>
                  <Value>PID-3-1=123456&PID-3-2=ED</Value>
                </Parameter>
               <Parameters>
             */
            if (this.Parameters == null) { return null; }

            List<string> result = new List<string>();
            foreach (Parameter p in this.Parameters)
            {
                if (p.Name.Equals(parameter, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(p.Value); //
                }
            }
            return result;
        }
        public string GetSettingValue(string parameterName, string defaultValue)
        {
            return GetFirstParameterVaule(parameterName) ?? defaultValue;
        }


        public string Description;
        public string DatabaseConnectionString; //SQL connection string
        public string Handler;     //handler class-name, it's fully qualified (with specified name-space)
        public string HandlerAssembly;     //the assembly the handler class resides in
        //public string DisposerClass;  //path for loading the custom class, 
        //eg "Foldda.Framework.HL7.MessageFilter" class from file "Foldda.Framework.HL7.dll"
        public string Encoding;
        public string CustomAlertPatterns;  //semi-colon separated regex pattern
        public string InitialState; //Start/Stop/Resume

        public Parameter[] Parameters; // name-value pairs, name format is "Class-name:parameter-name"

        public string ConfigFileFullPath { get; set; } = string.Empty;

        public override string ToString()
        {
            return Description;
        }

        internal List<string> GetAlertPatterns()
        {
            List<string> result = new List<string>();

            if (!string.IsNullOrEmpty(CustomAlertPatterns))
            {
                result.AddRange(CustomAlertPatterns.Split(';'));
            }

            return result;
        }

        public string Details()
        {
            System.Text.StringBuilder output = new System.Text.StringBuilder($"Handler={Handler};");
            if (!string.IsNullOrEmpty(DatabaseConnectionString)) { output.Append($"DatabaseConnectionString={DatabaseConnectionString};"); }
            if (!string.IsNullOrEmpty(Encoding)) { output.Append($"Encoding={Encoding};"); }
            if (!string.IsNullOrEmpty(CustomAlertPatterns)) { output.Append($"CustomAlertPatterns={CustomAlertPatterns};"); }
            //if (!string.IsNullOrEmpty(OptionArchive)) { output.Append($"OptionArchive={OptionArchive};"); }

            //node-type specific parrameters
            output.Append("Settings:");
            if (Parameters != null)
            {
                foreach (var p in Parameters)
                {
                    output.Append(p.Name).Append("=").Append(p.Value).Append("|");
                }
            }
            return output.ToString();
        }

        public char GetSettingValue(string parameterName, char defaultValue)
        {
            string charParamValue = GetFirstParameterVaule(parameterName);

            if(@"\t".Equals(charParamValue)) 
            { 
                return '\t'; 
            }
            else if(@"\\".Equals(charParamValue)) 
            { 
                return '\\'; 
            }
            else if(@"\'".Equals(charParamValue)) 
            { 
                return '\''; 
            }
            else if (char.TryParse(charParamValue, out char charValue) == true)
            {
                return charValue;
            }
            else
            {
                return defaultValue;
            }
        }
    }

    //defines extra parrameters required for the custom Processor class
    public class Parameter
    {
        public const char SEP_CHAR = (char)0x1f; //Constant.ASCII_US;
        public string Name;
        public string Value;    //index from

        public Parameter() { }  //required by serializer

        public Parameter(string nameValue)
        {
            int sepIndex = nameValue.IndexOf(SEP_CHAR);
            Name = nameValue.Substring(0, sepIndex);
            Value = nameValue.Substring(sepIndex + 1);
        }

        public string Serialised => ToString();

        public override string ToString()
        {
            return $"{Name}{SEP_CHAR}{Value}";
        }
    }
}
