using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Web.Script.Serialization;

namespace LessUACRunner.WinService
{
    /// <summary>
    /// This service is an application loading service that launches a GUI application into the current user's session.
    /// In addition, the application loads with full admin privileges, bypasses the Windows UAC prompt 
    /// and is executed with user environement variables.
    /// </summary>
    public partial class LoaderService : ServiceBase
    {
         public LoaderService()
        {
            InitializeComponent();
        }

        #region Types
        /// <summary>
        ///
        /// </summary>
        private class ControlData
        {
            public string FileName = null;
            public string Arguments = null;
            public bool Console = false;
            public string PipeName = null;
        }


        #endregion

        #region Constantes
        private const string controlNamedPipeName = "LESS_UAC_RUNNER-PIPE-CTL-B7C599BE-A605-4B72-8789-5CA074C86E69";
        #endregion     
        
        #region Fields
        private static Thread _lessUACRunnerThread;
        #endregion

        /// <summary>
        /// Start the thread: LessUACRunnerThread.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            Trace.WriteLine( string.Format("** OnStart at {0} **", DateTime.Now.ToString()));
            Trace.WriteLine( string.Format("OnStart: {0}: LessUACRunnerService.exe Version: {1}", DateTime.Now, Assembly.GetExecutingAssembly().GetName().Version));

            _lessUACRunnerThread = new Thread(new ThreadStart(LessUACRunnerThread));
            _lessUACRunnerThread.Name = "LessUACRunnerThread";
            _lessUACRunnerThread.IsBackground = true;
            _lessUACRunnerThread.Start();
        }
        /// <summary>
        /// 
        /// </summary>
        protected override void OnStop()
        {
            Trace.WriteLine( string.Format("** OnStop at {0} **", DateTime.Now.ToString()));
            Trace.WriteLine( string.Format("OnStop: _pipeThread state: {0}", _lessUACRunnerThread.ThreadState.ToString()));
            Trace.WriteLine( string.Format("OnStop: _pipeThread IsAlive: {0}", _lessUACRunnerThread.IsAlive));
        }

        /// <summary>
        /// This thread read in the LESS_UAC_RUNNER-PIPE-CTL-B7C599BE-A605-4B72-8789-5CA074C86E69, the application name to launch.
        /// Launch the application using ApplicationLoader class.
        /// </summary>
        private static void LessUACRunnerThread()
        {
            Trace.WriteLine( string.Format("{0}: LessUACRunnerThread: {1} thread ID= {2} launched", DateTime.Now, _lessUACRunnerThread.Name, _lessUACRunnerThread.ManagedThreadId));

            // Allow Everyone read and write access to the pipe.
            SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule pr = new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow);

            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(pr);

            while (true)
            {
                using (NamedPipeServerStream pipeServer =
                              new NamedPipeServerStream(
                                  controlNamedPipeName,
                                  PipeDirection.In,
                                  NamedPipeServerStream.MaxAllowedServerInstances,
                                  PipeTransmissionMode.Message,
                                  PipeOptions.None,
                                  1024,
                                  1024,
                                  ps,
                                  HandleInheritability.None))
                {
                    Trace.WriteLine("");
                    Trace.WriteLine("*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-");
                    Trace.WriteLine( string.Format("LessUACRunnerThread: NamedPipeServerStream object created."));

                    // Wait for a client to connect
                    Trace.WriteLine( string.Format("LessUACRunnerThread: Waiting for client connection... at {0}", DateTime.Now.ToString()));
                    try
                    {
                        pipeServer.WaitForConnection();
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine( string.Format("LessUACRunnerThread: LessUACRunnerThread.WaitForConnection() exception:  {0}", e.Message));
                    }
                    Trace.WriteLine( string.Format("LessUACRunnerThread: Client connected at {0}", DateTime.Now.ToString()));
                    using (StreamReader sr = new StreamReader(pipeServer))
                    {
                        // Read the pipe
                        string appToRun = string.Empty;
                        try
                        {
                            appToRun = sr.ReadLine();
                            if (appToRun == "-killThread") break;
                            ////////////////////////////////////////
                            Trace.WriteLine( string.Format("LessUACRunnerThread: ApplicationName : {0}", appToRun));
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine( string.Format("LessUACRunnerThread: ERROR: {0}", e.Message));
                        }

                        /////////////////////
                        // LAUNCH APPLICATION
                        ///////////////////////////
                        
                        ControlData controlData = new JavaScriptSerializer().Deserialize<ControlData>(appToRun);
                        if (controlData.Arguments != null) controlData.FileName += " " + controlData.Arguments;

                        //////////////// Multisessions
                        Thread worker;
                        worker = new Thread(new ThreadStart(new ApplicationLoader(controlData.FileName, controlData.Console, controlData.PipeName).StartProcessAndBypassUACThread));
                        worker.Name = "Worker-" + Guid.NewGuid().ToString();
                        worker.IsBackground = true;
                        worker.Start();
                        Trace.WriteLine(string.Format("LessUACRunnerThread: Worker thread launched:{0} DayTimeNow:{1}", worker.Name, DateTime.Now));
                        //////////////// Multisessions

                        //////////////// MonoSession
                        //Trace.WriteLine(string.Format("LessUACRunnerThread: appToRun:{0} console:{1} pipeName:{2}", controlData.FileName, controlData.Console, controlData.PipeName));
                        //new ApplicationLoader(controlData.FileName, controlData.Console, controlData.PipeName).StartProcessAndBypassUACThread();
                        ////////////////// MonoSession
                    }

                }
            }
            Trace.WriteLine( string.Format("** End thread {0} **", _lessUACRunnerThread.Name));
        }
    }

}
