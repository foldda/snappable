using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Foldda.Automation.Util
{
    public static class ConfigParser
    {
        private const string _key = "%!%%(9@#$";

        //encryot then encode in base64
        public static string Encrypt64(string text)
        {
            if (string.IsNullOrEmpty(text)) { return text; }
            return Base64Encode(EncryptOrDecrypt(text, _key));
        }

        public static string Decrypt64(string text)
        {
            return EncryptOrDecrypt(Base64Decode(text), _key);
        }

        private static string EncryptOrDecrypt(string text, string key)
        {
            var result = new StringBuilder();

            for (int c = 0; c < text.Length; c++)
                result.Append((char)((uint)text[c] ^ (uint)key[c % key.Length]));

            return result.ToString();
        }

        public static object Deserialize(Type type, string objectXmlFile)
        {
            using (FileStream fs = new FileStream(objectXmlFile, FileMode.Open,
                              FileAccess.Read, FileShare.ReadWrite))
            {
                TextReader reader = new StreamReader(fs);
                XmlSerializer serializer = new XmlSerializer(type);
                return serializer.Deserialize(reader);
            }
        }

        public static string SerializeToXml(object o)
        {
            StringBuilder sb = new StringBuilder();
            using (StringWriter sw = new StringWriter(sb))
            {
                XmlSerializer serializer = new XmlSerializer(o.GetType());
                serializer.Serialize(sw, o);
            }
            return sb.ToString();
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private enum ParsingState { Normal = 0, DoDecrypt = 1 }

        /**
         * This routine is used for restore partially-encrypted string eg password in ftp/database connection strings
         * 
         * Encrypt64() should be used to encrypt the string that needs to hide
         * 
         */
        //masked strings contains encrypted sub-strings which are enclosed with curly brakets  
        // eg "noraml string followed by encrypted {a732rfdd} and {23232~1}"
        //curly brakets will be removed, and strings withing curly brakets will be replaced with decrypted value,
        // output will be like "noraml string followed by encrypted DECRYPTED_STRING1 and DECRYPTED_STRING2"
        public static string DecryptMaskedString(string encrypted)
        {
            StringBuilder result = new StringBuilder();
            StringBuilder readBuf = new StringBuilder();
            ParsingState state = ParsingState.Normal;
            foreach (char c in encrypted.ToCharArray())
            {
                switch (state)
                {
                    case ParsingState.Normal:
                        {
                            if (c == '{')
                            {
                                state = ParsingState.DoDecrypt;
                                result.Append(readBuf);
                                readBuf.Remove(0, readBuf.Length);
                            }
                            readBuf.Append(c);
                            break;
                        }
                    case ParsingState.DoDecrypt:
                        {
                            if (c == '{')
                            {
                                result.Append(readBuf);    //hit another '{' (unexpectedly), disgard buffered
                                readBuf.Remove(0, readBuf.Length);//but stay in 'DoDecrypt' mode
                                readBuf.Append(c); //and restart buffering again for possible decryption
                            }
                            else if (c == '}')
                            {   //closing bracket found, start decryption
                                //NB, the enclosing { & } chars are ignored
                                if (readBuf.Length > 0)
                                {
                                    result.Append(Decrypt64(readBuf.Remove(0, 1).ToString()));    //decrypt and append to result
                                    readBuf.Remove(0, readBuf.Length);
                                }
                                state = ParsingState.Normal;
                            }
                            else
                            {
                                readBuf.Append(c);  //bufferring encrypted text
                            }
                            break;
                        }
                    default: break;
                }
            }

            if (readBuf.Length > 0) { result.Append(readBuf); } //scan finished, append the rest of the string to result.

            return result.ToString();
        }
    }
}
