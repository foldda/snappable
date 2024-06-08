using Charian;
using System.Collections.Generic;

namespace Foldda.DataAutomation.Framework
{
    public interface IConfigProvider
    {
        string ConfigProviderId { get; }

        string GetSettingValue(string parameterName, string defaultValue);  //a single string value with default 

        List<string> GetSettingValues(string parameterName);    //a list of string values

        int GetSettingValue(string parameterName, int defaultValue);    //an integer value with default 

        char GetSettingValue(string parameterName, char defaultValue);    //a char value with default 

        bool GetSettingValue(string parameterName, string valueToMatch, bool defaultResult);   //a boolean value with default 

        IRda GetSetting(string pname);   //this is a "catch-all" method get anything else from the context
    }
}