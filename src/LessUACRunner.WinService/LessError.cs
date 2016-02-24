using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LessUACRunner.WinService
{
    public enum ErrorCode
    {
        [Description("File not found")]
        ERROR_FileNotFound = 10000,
        [Description("Configuration file corrupted")]
        ERROR_FileCorrupted,
        [Description("Invalid arguments")]
        ERROR_InvalidArguments,
        [Description("Key already exist")]
        ERROR_KeyExist,
        [Description("Key does not exist")]
        ERROR_KeyNotExist,
        [Description("Unauthorized action")]
        ERROR_NotAllowed,
        [Description("Section already protected")]
        ERROR_SectionAlreadyProtected,
        [Description("Section still unprotected")]
        ERROR_SectionAlreadyNotProtected,
        [Description("n'a pas pu se connecter au serveur dans le timeout spécifié")]
        ERROR_NPConnectTimeOut,
        [Description("File not encrypted")]
        ERROR_FileNotCrypted,
        [Description("Service not found")]
        ERROR_ServiceNotInstalled,
    }

    public class LessError
    {
        // http://blog.spontaneouspublicity.com/associating-strings-with-enums-in-c
        public static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            return value.ToString();
        }

    }
}
