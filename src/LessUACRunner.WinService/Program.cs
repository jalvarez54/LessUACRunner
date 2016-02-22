using System.Diagnostics;
using System.ServiceProcess;

namespace LessUACRunner.WinService
{
    static class Program
    {
        static void Main()
        {
            Trace.WriteLine( "");
            Trace.WriteLine( ">>>>>>>>>>>>>>>>>>>>>");
            Trace.WriteLine( string.Format("Program Main:"));

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new LoaderService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
