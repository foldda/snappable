using Charian;
using System.Collections.Generic;

namespace Foldda.Automation.Framework
{
    public interface IConfigProvider
    {
        string ConfigFileFullPath { get; }   //full path/URL of the config file (optional, if provider is sourced based on a config file)

        string GetSettingValue(string parameterName, string defaultValue);  //a single string value with default 

        List<string> GetSettingValues(string parameterName);    //a list of string values

        int GetSettingValue(string parameterName, int defaultValue);    //an integer value with default 

        char GetSettingValue(string parameterName, char defaultValue);    //a char value with default 

        bool GetSettingValue(string parameterName, string valueToMatch, bool defaultResult);   //a boolean value with default 

        IRda GetSetting(string pname);   //this is a "catch-all" method get anything else from the context
    }
}