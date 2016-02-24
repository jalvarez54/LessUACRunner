using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LessUACRunner.WinService
{
    public enum MeErrorCode
    {
        [Description("fichier non trouvé")]
        ERROR_FileNotFound = 10000,
        [Description("fichier de configuration corrompu")]
        ERROR_FileCorrupted,
        [Description("nombre d'arguments incorrect")]
        ERROR_NumArguments,
        [Description("la clé existe déja")]
        ERROR_KeyExist,
        [Description("la clé n'existe pas")]
        ERROR_KeyNotExist,
        [Description("action non autorisée")]
        ERROR_NotAllowed,
        [Description("liste vide utiliser -configa pour ajouter une application")]
        ERROR_ListEmpty,
        [Description("section déja protégée")]
        ERROR_SectionAlreadyProtected,
        [Description("section déja non protégée")]
        ERROR_SectionAlreadyNotProtected,
        [Description("n'a pas pu se connecter au serveur dans le timeout spécifié")]
        ERROR_NPConnectTimeOut,
        [Description("le fichier n'est pas crypté")]
        ERROR_FileNotCrypted,
        [Description("le service est introuvable sur l'ordinateur")]
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
                return attributes[0].Description;
            else
                return value.ToString();

        }

    }
}
