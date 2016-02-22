using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Les informations générales relatives à un assembly dépendent de 
// l'ensemble d'attributs suivant. Changez les valeurs de ces attributs pour modifier les informations
// associées à un assembly.
[assembly: AssemblyTitle("LessUACRunner Service")]
[assembly: AssemblyDescription("Windows service that launches GUI application into the current user's session bypassing UAC.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("@APP_AUTHOR@")]
[assembly: AssemblyProduct("LessUACRunner")]
[assembly: AssemblyCopyright("Copyright © @APP_AUTHOR@ 2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// L'affectation de la valeur false à ComVisible rend les types invisibles dans cet assembly 
// aux composants COM.  Si vous devez accéder à un type dans cet assembly à partir de 
// COM, affectez la valeur true à l'attribut ComVisible sur ce type.
[assembly: ComVisible(false)]

// Le GUID suivant est pour l'ID de la typelib si ce projet est exposé à COM
[assembly: Guid("9be5de26-27ae-4ffe-ade6-7f398154992c")]

// Les informations de version pour un assembly se composent des quatre valeurs suivantes :
//
//      Version principale
//      Version secondaire 
//      Numéro de build
//      Révision
//
// Vous pouvez spécifier toutes les valeurs ou indiquer les numéros de build et de révision par défaut 
// en utilisant '*', comme indiqué ci-dessous :
[assembly: AssemblyFileVersion("@APP_VERSION@.@APP_RELEASE@")]
