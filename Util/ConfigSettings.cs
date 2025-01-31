using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Foldda.Automation.Util
{
    //...

    public class ConfigSettings
    {
        XDocument _xml = new XDocument();
        string _filePath = null;
        private static ConfigSettings _singleton = null;

        /*
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <appSettings>
            <!-- old style  
            <!-- assembly settings -->
            <add key="ROOT_NODES_PATH" value="D:\_DATA\Nodes"/>
            <add key="EMAIL_RECIPIENTS" value="someone@gmail.com"/>
            -->

            <!-- new style, can have multi values per key (id), and can filter on value attr -->
            <key id="bus">
              <value>VW</value>
            </key>
            <key id="car">
              <value attr="fast">Farrari</value>
              <value attr="slow">Tata</value>
            </key>
            <key id="truck">
              <value>Ford</value>
            </key>
          </appSettings>
        </configuration>
        */
        static ConfigSettings()
        {
            try { _singleton = new ConfigSettings(getConfigFilePath()); }
            catch { _singleton = null; }
        }

        private ConfigSettings(string configFilePath)
        {
            try
            {
                _xml = XDocument.Load(configFilePath);
                _filePath = configFilePath;
            }
            catch
            {
                throw new InvalidConfigException($"Cannot load config file [{configFilePath}], please ensure config file is valid.");
            }
        }

        private static string getConfigFilePath()
        {
            //GetExecutingAssembly will get the DLL
            string path = Assembly.GetEntryAssembly().Location + ".config"; //
            if (!File.Exists(path))
            {
                TextWriter tw = new StreamWriter(path);
                string emptyConfigFileContent =
@"<?xml version=""1.0""?>
<configuration>
  <appSettings>
  </appSettings>
</configuration>";
                // create an empty config file
                tw.WriteLine(emptyConfigFileContent);

                // close the stream
                tw.Close();
            }
            return path;
        }


        public static string Get(string name)
        {
            try
            {
                return _singleton?.GetAll(name, null).LastOrDefault();//linq ext
            }
            catch
            {
                //throw new InvalidConfigException($"Cannot get setting for parameter [{name}], please ensure config file is valid.");
            }
            return null;
        }

        public static List<string> GetSaved(string name, string attribute)
        {
            try
            {
        
                return _singleton?.GetAll(name, attribute);
            }
            catch
            {
                return null;
            }
        }

        public static void Save(string name, string valueAttribute, string value, int keepLastN)
        {
            List<string> saved = GetSaved(name, valueAttribute);
            if(saved != null && saved.Count> 0)
            {
                //no need to save if it's already there.
                foreach(var existing in GetSaved(name, valueAttribute))
                {
                    if (existing.Equals(value)) { return; }
                }
            }

            _singleton?.SavePrivate(name, valueAttribute, value, keepLastN);
        }

        public static void Remove(string name, string value)
        {
            _singleton?.RemovePrivate(name, value);
        }

        private void RemovePrivate(string name, string value)
        {
            XElement config = _xml.Root.XPathSelectElement(CONFIG_XPATH);
            string xpath = $"//{KEY}[@{KEY_ID}='{name}']/{VALUE}";
            foreach (var item in config.XPathSelectElements(xpath))
            {
                if (item.Value.Equals(value)) { item.Remove(); }
            }
            _xml.Save(_filePath);
        }

        static readonly string CONFIG_XPATH = "/configuration/appSettings", 
            KEY = "key", VALUE = "value", KEY_ID = "id", VALUE_ATTR = "attr";

        private List<string> GetAll(string keyId, string valueAttribute)
        {
            List<string> result = new List<string>();
            XElement config = _xml.Root.XPathSelectElement(CONFIG_XPATH);
            string xpath = $"//{KEY}[@{KEY_ID}='{keyId}']/{VALUE}";

            if (!string.IsNullOrEmpty(valueAttribute))
            {
                xpath += $"[@{VALUE_ATTR}='{valueAttribute}']";
            }

            foreach (var item in config.XPathSelectElements(xpath))
            {
                result.Add(item.Value);
            }
            return result;
        }

        private void SavePrivate(string keyId, string valueAttribute, string value, int keepLastN)
        {
            XElement config = _xml.Root.XPathSelectElement(CONFIG_XPATH);
            if(config==null)
            {
                string[] tokens = CONFIG_XPATH.Split('/');
                XElement nextLevel = _xml.Root;
                foreach(var tok in tokens)
                {
                    if(!string.IsNullOrEmpty(tok))
                    {
                        XElement newElement = nextLevel.XPathSelectElement($"/{tok}");
                        if (newElement == null)
                        {
                            newElement = new XElement(tok);
                            nextLevel.Add(newElement);
                            nextLevel = newElement;
                        }
                    }
                }
                config = nextLevel;
            }

            XElement key = config.XPathSelectElement($"//{KEY}[@{KEY_ID}='{keyId}']");
            if (key == null)
            {
                key = new XElement(KEY, new XAttribute(KEY_ID, keyId));
                config.Add(key);
            }
            XElement valueElement = string.IsNullOrEmpty(valueAttribute) ? new XElement(VALUE) :
            new XElement(VALUE, new XAttribute(VALUE_ATTR, valueAttribute));
            valueElement.Value = value;
            key.Add(valueElement);

            int keepN = keepLastN < 0 ? 0 : keepLastN;
            while (key.Descendants(VALUE).Count() > keepN)
            {
                key.Descendants(VALUE).First().Remove();
            }

            //save the xml structure to file, overwrite existing
            _xml.Save(_filePath);
        }

    }
}
