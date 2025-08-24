using System;
using System.Windows.Forms;

namespace DoW_DE_Nod_Manager
{
    public static class Extensions
    {
        /// <summary>
        /// This method gets a value from a line of text
        /// </summary>
        /// <param name="deleteModule"></param>
        /// <returns>string</returns>
        public static string GetValueFromLine(this string line, bool deleteModule)
        {
            int indexOfEqualSigh = line.IndexOf('=');

            if (indexOfEqualSigh > 0)
            {
                // Deleting all chars before equal sigh
                line = line.Substring(indexOfEqualSigh + 1, line.Length - indexOfEqualSigh - 1);

                if (line.Contains("\\"))
                    return line;

                if (deleteModule)
                    return line.Replace(" ", "").Replace(".module", "");
                else
                    return line.Replace(" ", "");
            }
            else
                return "";
        }

        /// <summary>
        /// This method gets a setting from a line of text
        /// </summary>
        /// <returns>string</returns>
        public static string GetSettingFromLine(this string line)
        {
            int indexOfEqualSigh = line.IndexOf('=');

            if (indexOfEqualSigh > 0)
            {
                // Deleting all chars after equal sigh
                line = line.Substring(0, indexOfEqualSigh);

                return line.Replace(" ", "");
            }
            else
                return "";
        }

        public static (string, string) GetSettingAndValueFromLine(this string line, bool removeComma)
        {
            int indexOfEqualSigh = line.IndexOf('=');
            string setting;
            string value;

            if (indexOfEqualSigh > 0)
            {
                setting = line.Substring(0, indexOfEqualSigh);
                value = line.Substring(indexOfEqualSigh + 1, line.Length - indexOfEqualSigh - 1);
                
                setting = setting.Replace(" ", "");
                value = value.Replace(" ", "");

                if (removeComma)
                    value = value.Replace(",", "");

                return (setting, value);
            }
            else
                return ("", "");
        }
    }
}
