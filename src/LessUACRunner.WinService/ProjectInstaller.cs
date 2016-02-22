using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;

namespace LessUACRunner.WinService
{
    [RunInstaller(true)]
    public class ProjectInstaller : System.Configuration.Install.Installer
    {
        const string serviceName = "LessUACRunnerService";

        public ProjectInstaller()
        {
            MyInitializeComponent();
        }
        
        private System.ServiceProcess.ServiceProcessInstaller loaderServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller loaderServiceInstaller;
        private void MyInitializeComponent()
        {

            this.loaderServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.loaderServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // loaderServiceProcessInstaller
            // 
            this.loaderServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.loaderServiceProcessInstaller.Password = null;
            this.loaderServiceProcessInstaller.Username = null;
            // 
            // loaderServiceInstaller
            // 
            this.loaderServiceInstaller.Description = System.Configuration.ConfigurationManager.AppSettings["ServiceDescription"];
            this.loaderServiceInstaller.DisplayName = System.Configuration.ConfigurationManager.AppSettings["ServiceDisplayName"];
            this.loaderServiceInstaller.ServiceName = serviceName;
            this.loaderServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.loaderServiceProcessInstaller,
            this.loaderServiceInstaller});
        }

        // Override the 'Install' method of the Installer class.
        public override void Install(IDictionary mySavedState)
        {
            base.Install(mySavedState);
            Trace.WriteLine( string.Format("{0}: Service.Install", DateTime.Now));

            System.Collections.Specialized.StringDictionary myStringDictionary = Context.Parameters;
            if (Context.Parameters.Count > 0)
            {
                Trace.WriteLine( string.Format("{0}: Service.Install: {1}", DateTime.Now, "Context Property: "));

                // http://stackoverflow.com/questions/1327271/c-sharp-stringdictionary-how-to-get-keys-and-values-using-a-single-loop
                foreach (DictionaryEntry item in myStringDictionary)
                {
                    Trace.WriteLine( string.Format("{0}: Service.Install: key = {1} value = {2}", DateTime.Now, item.Key, item.Value));
                }
            }
        }
    }
}
