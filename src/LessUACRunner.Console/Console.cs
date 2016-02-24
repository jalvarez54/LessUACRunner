using Microsoft.Win32;
using System;
using System.Collections;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Web.Script.Serialization;

namespace LessUACRunner.Console
{
    /// <summary>
    /// This command send to the LessUACRunner.WinService, the application name to launch. 
    /// http://stackoverflow.com/questions/3563744/how-can-i-hide-a-console-window
    /// </summary>
    class Program
    {
        #region Constants
        private const string commandExeName = "LessUACRunnerConsole.exe";
        private const string serviceExeName = "LessUACRunnerService.exe";
        private const string pipeName = "LESS_UAC_RUNNER-PIPE-CTL-B7C599BE-A605-4B72-8789-5CA074C86E69";
        private const string sectionName = "lessUACRunner";
        #endregion

        #region Types
        private class ProcessReturn
        {
            public string StdError = null;
            public string StdOut = null;
            public int ExitCode = 0;
            public string ElapsedTime = null;
        }

        private class ControlData
        {
            public string FileName = null;
            public string Arguments = null;
            public bool Console = false;
            public string PipeName = null;
        }


        #endregion

        #region Static fields
        private static ProcessReturn _processReturnObject;
        private static string _commandPath;
        private static string _servicePath;
        private static ServiceController _serviceController;
        private static int _argCount;
        private static string _baseDirectory;
        private static string _serviceName;
        private static string _serviceInstallerLogFileName;
        private static string _logsFolderName;
        private static string _ConsoleAssemblyName;
        private static string _ConsoleProductVersion;
        private static string _ConsoleAssemblyVersion;
        private static string _ConsoleAssemblyFileVersion;
        private static string _ServiceAssemblyName;
        private static string _ServiceProductVersion;
        private static string _ServiceAssemblyVersion;
        private static string _ServiceAssemblyFileVersion;
        private static bool _console;
        private static int _commandExitCode = 0;
        private static string _jsonReturnedData;
        private static TraceSwitch _traceSwitch;
        private static Configuration _configuration;
        private static AllowedAppsSection _section;

        #endregion

        #region MAIN
        /// <summary>
        /// Parse command line and call WriteApplicationNameInPipe method.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        static int Main(string[] args)
        {
            TraceInit();

            _configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            try
            {
                _section = _configuration.GetSection(sectionName) as AllowedAppsSection;
            }
            catch (Exception)
            {
                SetAndShowError("Main", WinService.ErrorCode.ERROR_FileCorrupted, true);
                return -1;
            }
            _section = _configuration.GetSection(sectionName) as AllowedAppsSection;

            #region Only infos
            _baseDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            _commandPath = string.Format(@"{0}\{1}", _baseDirectory, commandExeName);
            _servicePath = string.Format(@"{0}\{1}", _baseDirectory, serviceExeName);
            _logsFolderName = "logs";
            _serviceName = System.Configuration.ConfigurationManager.AppSettings["ServiceDisplayName"];
            _serviceInstallerLogFileName = "serviceInstall.log";
            _serviceController = new ServiceController(_serviceName);

            Trace.WriteLineIf(_traceSwitch.TraceVerbose, ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: OS : {1}", DateTime.Now, Environment.OSVersion.ToString()));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: .NET Framework : {1}", DateTime.Now, FrameWorkVersion()));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: CLR : {1}", DateTime.Now, Environment.Version.ToString()));
            try
            {
                _ConsoleAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                _ServiceAssemblyVersion = Assembly.LoadFrom(_servicePath).GetName().Version.ToString();

                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                _ConsoleAssemblyFileVersion = fvi.FileVersion;
                _ConsoleProductVersion = fvi.ProductVersion;
                _ConsoleAssemblyName = fvi.ProductName;

                Assembly assemblys = Assembly.LoadFrom(_servicePath);
                FileVersionInfo fvis = FileVersionInfo.GetVersionInfo(assemblys.Location);
                _ServiceAssemblyFileVersion = fvis.FileVersion;
                _ServiceProductVersion = fvis.ProductVersion;
                _ServiceAssemblyName = fvis.ProductName;

                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console Assembly Name: {1}", DateTime.Now, _ConsoleAssemblyName));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console Product Version: {1}", DateTime.Now, _ConsoleProductVersion));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console Assembly Version: {1}", DateTime.Now, _ConsoleAssemblyVersion));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console Assembly File Version: {1}", DateTime.Now, _ConsoleAssemblyFileVersion));

                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Service Assembly Name: {1}", DateTime.Now, _ServiceAssemblyName));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Service Product Version: {1}", DateTime.Now, _ServiceProductVersion));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Service Assembly Version: {1}", DateTime.Now, _ServiceAssemblyVersion));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Service Assembly File Version: {1}", DateTime.Now, _ServiceAssemblyFileVersion));
            }
            catch (Exception ex)
            {
                ShowMessage("Main", ex.Message, false);
            }
            #endregion

            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console.Main User: {1}", DateTime.Now, Environment.UserName));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console.Main IsAdmin ?: {1}", DateTime.Now, IsAdmin()));

            if (!IsAdmin())
            {
                // If not encypted permit only -encrypt, -help and -install
                if (!IsEncrypted() && args[0] != "-encrypt" && args[0] != "-help" && args[0] != "-install")
                {
                    SetAndShowError("Main", WinService.ErrorCode.ERROR_FileNotCrypted, true);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console.Main IsEncrypted ?: {1}", DateTime.Now, IsAdmin()));
                    return _commandExitCode;
                }
            }

            if (!IsInstalled())
            {
                SetAndShowError("Main", WinService.ErrorCode.ERROR_ServiceNotInstalled, true);
                ShowMessage("WARNING: Service not installed\n");
            }

            _argCount = args.Count();
            if (_argCount > 0)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: Console.Main Choice: {1}", DateTime.Now, args[0]));

                switch (args[0])
                {
                    case "-configa":
                        ChoiceConfigAdd(args);
                        break;
                    case "-configd":
                        ChoiceConfigDelete(args);
                        break;
                    case "-start":
                        ChoiceStart();
                        break;
                    case "-stop":
                        ChoiceStop();
                        break;
                    case "-restart":
                        ChoiceRestart();
                        break;
                    case "-install":
                        ChoiceInstall();
                        break;
                    case "-uninstall":
                        ChoiceUnInstall();
                        break;
                    case "-encrypt":
                        ChoiceEncryptDecrypt(true);
                        break;
                    case "-decrypt":
                        ChoiceEncryptDecrypt(false);
                        break;
                    case "-list":
                        ChoiceList();
                        break;
                    case "-status":
                        ChoiceStatus();
                        break;
                    case "-help":
                        ChoiceHelp();
                        break;
                    case "-version":
                        ChoiceVersion();
                        break;
                    default:
                        ChoiceRunTheProcess(args);
                        break;
                }
            }
            else
            {
                ChoiceHelp();
            }

            if (_console)
            {
                System.Console.WriteLine(_jsonReturnedData);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ElapsedTime on Windows Service: {1}", DateTime.Now, _processReturnObject.ElapsedTime));
            }

            return (_commandExitCode);
        }

        /// <summary>
        /// Return data read on pipe
        /// </summary>
        /// <param name="pipeName"></param>
        /// <returns>On error return string empty</returns>
        private static string ReadReturnFromPipe(string pipeName)
        {
            string returnData = string.Empty;

            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In))
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe attempting to connect to pipe...: {1}", DateTime.Now, pipeName));

                    // Connect to the pipe or wait until the pipe is available.
                    string timeOut = System.Configuration.ConfigurationManager.AppSettings["PipeConnectTimeOut"];
                    try
                    {
                        //pipeClient.Connect(int.Parse("20000"));
                        pipeClient.Connect();
                    }
                    catch (System.TimeoutException ex)
                    {
                        SetAndShowError("ReadReturnFromPipe", WinService.ErrorCode.ERROR_NPConnectTimeOut, true);
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe exception: {1}", DateTime.Now, ex.Message));
                        return string.Empty;
                    }
                    catch (Exception ex)
                    {
                        ShowMessage("ReadReturnFromPipe", ex.Message, false);
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe exception: {1}", DateTime.Now, ex.Message));
                        _commandExitCode = -1;
                        return string.Empty;
                    }

                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe connected to pipe: {1}", DateTime.Now, pipeName));

                    // Read in the pipe proces return.
                    using (StreamReader sr = new StreamReader(pipeClient))
                    {
                        returnData = sr.ReadToEnd();
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe read data: {1}", DateTime.Now, returnData));

                    }
                }

            }
            catch (Exception ex)
            {
                ShowMessage("ReadReturnFromPipe", ex.Message, false);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ReadReturnFromPipe exception: {1}", DateTime.Now, ex.Message));
                _commandExitCode = -1;
                return string.Empty;
            }
            return returnData;

        }

        /// <summary>
        /// Write in the pipe LESS_UAC_RUNNER-PIPE-CTL-B7C599BE-A605-4B72-8789-5CA074C86E69 the applicaion name to launch.
        /// https://msdn.microsoft.com/en-us/library/system.io.pipes.namedpipeclientstream%28v=vs.110%29.aspx
        /// </summary>
        /// <param name="applicationName"></param>
        /// <returns>on error return false, else true</returns>
        private static bool WriteApplicationNameInPipe(string applicationName)
        {
            using (NamedPipeClientStream pipeClient =
                new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: WriteApplicationNameInPipe attempting to connect to pipe...: {1}", DateTime.Now, pipeName));

                // Connect to the pipe or wait until the pipe is available.
                string timeOut = System.Configuration.ConfigurationManager.AppSettings["PipeConnectTimeOut"];
                try
                {
                    pipeClient.Connect(int.Parse(timeOut));
                }
                catch (System.TimeoutException)
                {
                    SetAndShowError("WriteApplicationNameInPipe", WinService.ErrorCode.ERROR_NPConnectTimeOut, true);
                    return false;
                }
                catch (Exception ex)
                {
                    ShowMessage("WriteApplicationNameInPipe", ex.Message, false);
                    _commandExitCode = -1;
                    return false;
                }

                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: WriteApplicationNameInPipe connected to pipe: {1}", DateTime.Now, pipeName));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: WriteApplicationNameInPipe writing in the pipe: {1}", DateTime.Now, applicationName));

                // Write application name in the pipe and send that to the server process.
                using (StreamWriter sw = new StreamWriter(pipeClient))
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(applicationName);
                }
            }
            return true;
        }
        #endregion

        #region Command line parser methods
        /// <summary>
        /// -configa app_path
        /// -configa shortcut app_path
        /// -configa app_path app_args
        /// </summary>
        /// <param name="args">[shortcut] app_path</param>
        private static void ChoiceConfigAdd(string[] args)
        {
            if (IsAdmin())
            {
                AllowedAppElement ae = new AllowedAppElement();

                if (_argCount < 2)
                {
                    SetAndShowError("ChoiceConfigAdd", WinService.ErrorCode.ERROR_InvalidArguments, true);
                    return;
                }
                // -configa [shortcut] app_path [app_args] [-console]
                ae.Console = args.Contains<string>("-console");

                // -configa app_path [app_args] [-console]
                if (File.Exists(args[1]))
                {
                    ae.Shortcut = Path.GetFileNameWithoutExtension(args[1]);
                    ae.Path = args[1];
                    ae.Args = (_argCount == 3) ? args[2] : string.Empty;
                }
                // -configa [shortcut] app_path [app_args] [-console]
                else
                {
                    if (_argCount < 3)
                    {
                        SetAndShowError("ChoiceConfigAdd", WinService.ErrorCode.ERROR_InvalidArguments, true);
                        return;
                    }
                    // -configa [shortcut] app_path [app_args] [-console]
                    if (File.Exists(args[2]))
                    {
                        ae.Path = args[2];
                        ae.Shortcut = args[1];
                        ae.Args = (_argCount == 4) ? args[3] : string.Empty;
                    }
                    else
                    {
                        SetAndShowError("ChoiceConfigAdd", WinService.ErrorCode.ERROR_FileNotFound, true);
                        return;
                    }
                }

                if (_section.AllowedApps[ae.Shortcut] == null)
                {
                    _section.AllowedApps.Add(ae);
                    _configuration.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("sectionName");
                }
                else
                {
                    SetAndShowError("ChoiceConfigAdd", WinService.ErrorCode.ERROR_KeyExist, true);
                    return;
                }
            }
            else
            {
                SetAndShowError("ChoiceConfigAdd", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }

        }

        private static void ChoiceConfigDelete(string[] args)
        {
            if (_argCount != 2)
            {
                SetAndShowError("ChoiceConfigDelete", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                if (_section.AllowedApps[args[1]] != null)
                {
                    _section.AllowedApps.Remove(_section.AllowedApps[args[1]]);
                    _configuration.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection(sectionName);
                }
                else
                {
                    SetAndShowError("ChoiceConfigDelete", WinService.ErrorCode.ERROR_KeyNotExist, true);
                    return;
                }
            }
            else
            {
                SetAndShowError("ChoiceConfigDelete", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }
        }

        private static void ChoiceStart()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceStart", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                try
                {
                    if ((_serviceController.Status.Equals(ServiceControllerStatus.Stopped)) ||
                    (_serviceController.Status.Equals(ServiceControllerStatus.StopPending)))
                    {
                        ShowMessage("ChoiceStart", "Starting service...", true);
                        _serviceController.Start();
                        _serviceController.WaitForStatus(ServiceControllerStatus.Running);
                        ShowMessage("ChoiceStart", "Service status: " + _serviceController.Status, true);
                    }
                    else
                    {
                        ShowMessage("ChoiceStart", "Service already started", true);
                    }
                }
                catch (Exception e)
                {
                    ShowMessage("ChoiceStart", "exception " + e.Message, false);
                    _commandExitCode = -1;
                }
            }
            else
            {
                SetAndShowError("ChoiceStart", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }
        }

        private static void ChoiceStop()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceStop", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                try
                {
                    if ((_serviceController.Status.Equals(ServiceControllerStatus.Stopped)) ||
                    (_serviceController.Status.Equals(ServiceControllerStatus.StopPending)))
                    {
                        ShowMessage("ChoiceStop", "Service already stopped", true);
                    }
                    else
                    {
                        // Stop the service if its status is not set to "Stopped".
                        ShowMessage("ChoiceStart", "Stopping service...", true);
                        WriteApplicationNameInPipe("-killThread");
                        _serviceController.Stop();
                        _serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                        ShowMessage("ChoiceStop", "Service status: " + _serviceController.Status, true);
                    }
                }
                catch (Exception e)
                {
                    ShowMessage("ChoiceStop", "exception " + e.Message, false);
                    _commandExitCode = -1;
                }
            }
            else
            {
                SetAndShowError("ChoiceStop", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }
        }

        private static void ChoiceRestart()
        {
            ChoiceStop();
            ChoiceStart();
        }

        private static void ChoiceInstall()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceInstall", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                if (!IsInstalled())
                {
                    try
                    {
                        InstallService();

                    }
                    catch (Exception e)
                    {
                        ShowMessage("ChoiceInstall", "exception " + e.Message, false);
                        _commandExitCode = -1;
                        return;
                    }
                    try
                    {
                        StartService();

                    }
                    catch (Exception e)
                    {
                        ShowMessage("ChoiceInstall", "exception " + e.Message, false);
                        _commandExitCode = -1;
                        return;
                    }
                }
                else
                {
                    ShowMessage("ChoiceInstall", "Service already installed", true);
                    return;
                }
            }
            else
            {
                SetAndShowError("ChoiceInstall", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }

        }

        private static void ChoiceUnInstall()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceUnInstall", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                if (IsInstalled())
                {
                    try
                    {
                        StopService();
                    }
                    catch (Exception)
                    {
                    }
                    try
                    {
                        UninstallService();
                    }
                    catch (Exception e)
                    {
                        ShowMessage("ChoiceInstall", "exception " + e.Message, false);
                        _commandExitCode = -1;
                        return;
                    }
                }
                else
                {
                    ShowMessage("ChoiceUnInstall", "Service already uninstalled", true);
                    return;
                }
            }
            else
            {
                SetAndShowError("ChoiceUnInstall", WinService.ErrorCode.ERROR_NotAllowed, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encrypt">true=encrypt ; false=decrypt</param>
        private static void ChoiceEncryptDecrypt(bool encrypt)
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceEncryptDecrypt", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }

            if (IsAdmin())
            {
                System.Reflection.Assembly asem = System.Reflection.Assembly.GetExecutingAssembly();
                string fileName = asem.Location;
                if (File.Exists(fileName))
                {
                    EncryptDecryptAppSettings(encrypt, fileName);
                }
                else
                {
                    SetAndShowError("ChoiceEncryptDecrypt", WinService.ErrorCode.ERROR_FileNotFound, true);
                }
            }
            else
            {
                SetAndShowError("ChoiceEncryptDecrypt", WinService.ErrorCode.ERROR_NotAllowed, true);
                return;
            }
        }

        private static void ChoiceList()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceList", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }
            ShowMessage("List of shortcuts (allowed applications):");
            if (_section.AllowedApps.Count != 0)
            {
                foreach (AllowedAppElement item in _section.AllowedApps)
                {
                    ShowMessage(string.Format("{0} : {1}", item.Shortcut, item.Path));
                }
            }
            else
            {
                System.Console.WriteLine("N/A");
            }
        }

        private static void ChoiceStatus()
        {
            if (_argCount != 1)
            {
                SetAndShowError("ChoiceStatus", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }
            try
            {
                ShowMessage(string.Format("{0} service status:\n  Installed: {1}\n  Status: {2}", _serviceController.ServiceName, IsInstalled(), _serviceController.Status));
            }
            catch (Exception ex)
            {
                ShowMessage("ERROR: " + ex.Message);
                _commandExitCode = -1;
            }
        }

        private static void ChoiceHelp()
        {
            System.Console.WriteLine("LessUACRunner v" + _ConsoleProductVersion);
            System.Console.WriteLine("Usage: " + commandExeName + " [OPTION|SHORTCUT]");

            System.Console.WriteLine("\nBasic privileges:");
            System.Console.WriteLine("  -help    : {0}", "Get help");
            System.Console.WriteLine("  -version : {0}", "Get version infos");
            System.Console.WriteLine("  -status  : {0}", "Service status");
            System.Console.WriteLine("  -list    : {0}", "List of shortcuts (allowed applications)");
            System.Console.WriteLine("  shortcut : {0}", "App to execute (must be added in App.config via -configa)");
            System.Console.WriteLine("             examples :");
            System.Console.WriteLine("               " + commandExeName + " {0}", "shortcut");
            System.Console.WriteLine("               " + commandExeName + " {0}", "shortcut \"file arguments\"");

            System.Console.WriteLine("\nAdministrator privileges:");
            System.Console.WriteLine("  -configa   : {0}", "Add shortcut, file path and arguments in App.config");
            System.Console.WriteLine("               args     : {0}", "[shortcut] app_path [app_args] [-console]");
            System.Console.WriteLine("                 app_path : {0}", "path to the executable application");
            System.Console.WriteLine("                 app_args : {0}", "arguments for your application");
            System.Console.WriteLine("                 -console : {0}", "is console application");
            System.Console.WriteLine("               examples :");
            System.Console.WriteLine("                 -configa {0}", "app_path");
            System.Console.WriteLine("                 -configa {0}", "shortcut app_path");
            System.Console.WriteLine("                 -configa {0}", "app_path app_args");
            System.Console.WriteLine("                 -configa {0}", "shortcut app_path app_args");
            System.Console.WriteLine("  -configd   : {0}", "Delete file path (and arguments) from App.config");
            System.Console.WriteLine("               args : {0}", "shortcut");
            System.Console.WriteLine("  -start     : {0}", "Start the service");
            System.Console.WriteLine("  -stop      : {0}", "Stop the service");
            System.Console.WriteLine("  -restart   : {0}", "Stop and Start the service");
            System.Console.WriteLine("  -install   : {0}", "Install service");
            System.Console.WriteLine("  -uninstall : {0}", "Uninstall service");
            System.Console.WriteLine("  -encrypt   : {0}", "Encrypt App.config");
            System.Console.WriteLine("  -decrypt   : {0}", "Decrypt App.config");
        }

        private static void ChoiceVersion()
        {
            System.Console.WriteLine("LessUACRunner");
            System.Console.WriteLine("  Console : v{0}", _ConsoleProductVersion);
            System.Console.WriteLine("  Service : v{0}", _ServiceProductVersion);

            System.Console.WriteLine("System");
            System.Console.WriteLine("  OS             : {0}", Environment.OSVersion.ToString());
            System.Console.WriteLine("  .NET Framework : {0}", FrameWorkVersion());
            System.Console.WriteLine("  CLR            : {0}", Environment.Version.ToString());
        }

        private static void ChoiceRunTheProcess(string[] args)
        {
            if (!IsInstalled())
            {
                return;
            }
            if (_argCount > 2)
            {
                SetAndShowError("ChoiceRunTheProcess", WinService.ErrorCode.ERROR_InvalidArguments, true);
                return;
            }
            try
            {
                bool isShortcutExist = false;
                foreach (AllowedAppElement item in _section.AllowedApps)
                {
                    if (item.Shortcut == args[0]) isShortcutExist = true;
                }
                if (!isShortcutExist)
                {
                    SetAndShowError("ChoiceRunTheProcess", WinService.ErrorCode.ERROR_NotAllowed, true);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess Sorry you cannot run this application in admin mode !", DateTime.Now));
                    return;
                }
                else
                {
                    // _section.AllowedApps[args[0]].Path = app_path [-console]
                    // Add (DNP) with GUID (LESS_UAC_RUNNER-PIPE-DAT-XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX) if -console
                    string pipeName = "LESS_UAC_RUNNER-PIPE-DAT-" + Guid.NewGuid();

                    ControlData controlData = new ControlData();
                    controlData.FileName = _section.AllowedApps[args[0]].Path;
                    controlData.Console = _section.AllowedApps[args[0]].Console;
                    controlData.PipeName = _section.AllowedApps[args[0]].Console ? pipeName : null;
                    controlData.Arguments = _section.AllowedApps[args[0]].Args;

                    // Hook stored arguments if dynamicArgument != string.Empty
                    // Inline arguments to console #1 
                    string dynamicArgument = args.Length == 2 ? args[1] : string.Empty;
                    controlData.Arguments = dynamicArgument == "" ? _section.AllowedApps[args[0]].Args : dynamicArgument;

                    if (File.Exists(_section.AllowedApps[args[0]].Path))
                    {
                        //////// WRITE PROCESS NAME TO LAUNCHED IN CNP)
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess calling WriteApplicationNameInPipe arg={1}", DateTime.Now, _section.AllowedApps[args[0]].Path));

                        var json = new JavaScriptSerializer().Serialize(controlData);
                        if (WriteApplicationNameInPipe(json))
                        {
                            ///////////////////////////////////////////////////////////////////////

                            //////// READ PROCESS RETURN FROM DNP)
                            // If -console we must connect to (DNP) / Read the content / Print the content to stdout / exit with PROCESS exitcode
                            _console = _section.AllowedApps[args[0]].Console;
                            if (_console)
                            {
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess calling ReadReturnFromPipe arg={1}", DateTime.Now, pipeName));

                                _jsonReturnedData = ReadReturnFromPipe(pipeName);
                                if (_jsonReturnedData != string.Empty)
                                {
                                    // Extraction:
                                    _processReturnObject = new JavaScriptSerializer().Deserialize<ProcessReturn>(_jsonReturnedData);
                                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess read returnedData: {1}", DateTime.Now, _jsonReturnedData));
                                }
                                else
                                {
                                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess nothing returned or error", DateTime.Now));
                                    return;
                                }
                            }
                            /////////////////////////////////////////////////////////////////////// 
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        SetAndShowError("ChoiceRunTheProcess", WinService.ErrorCode.ERROR_FileNotFound, true);
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess file unreachable: {1}", DateTime.Now, _section.AllowedApps[args[0]].Path));
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                ShowMessage("ChoiceRunTheProcess", e.Message, false);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: ChoiceRunTheProcess exception: {1}", DateTime.Now, e.Message));
                _commandExitCode = -1;
            }
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("{0}: >>>>> ChoiceRunTheProcess job ended successfully", DateTime.Now));
        }
        #endregion

        #region Service utils
        // http://svn.soapboxsnap.com/svn/trunk/SoapBox/SoapBox.Snap/SoapBox.Snap.Runtime.Service/ServiceManager.cs
        private static bool IsInstalled()
        {
            using (ServiceController controller =
                new ServiceController(_serviceName))
            {
                try
                {
                    ServiceControllerStatus status = controller.Status;
                }
                catch
                {
                    return false;
                }
                return true;
            }
        }

        private static bool IsRunning()
        {
            using (ServiceController controller =
                new ServiceController(_serviceName))
            {
                if (!IsInstalled()) return false;
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private static AssemblyInstaller GetInstaller()
        {
            // https://msdn.microsoft.com/en-us/library/system.configuration.install.assemblyinstaller.install%28v=vs.110%29.aspx
            // Set the commandline argument array for 'logfile'.
            string[] myString = new string[1];
            myString[0] = string.Format("/logFile={0}{1}\\{2}", _baseDirectory, _logsFolderName, _serviceInstallerLogFileName);
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, myString[0]);
            AssemblyInstaller installer = new AssemblyInstaller(
                typeof(WinService.LoaderService).Assembly, null);
            installer.CommandLine = myString;
            installer.UseNewContext = true;
            return installer;
        }

        private static void InstallService()
        {
            if (IsInstalled()) return;

            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();

                    try
                    {
                        installer.Install(state);
                        installer.Commit(state);
                    }
                    catch
                    {
                        try
                        {
                            installer.Rollback(state);
                        }
                        catch { }
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private static void UninstallService()
        {
            if (!IsInstalled()) return;
            try
            {
                using (AssemblyInstaller installer = GetInstaller())
                {
                    IDictionary state = new Hashtable();
                    try
                    {
                        installer.Uninstall(state);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private static void StartService()
        {
            if (!IsInstalled()) return;

            using (ServiceController controller =
                new ServiceController(_serviceName))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Running)
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }

        private static void StopService()
        {
            if (!IsInstalled()) return;
            using (ServiceController controller =
                new ServiceController(_serviceName))
            {
                try
                {
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped,
                             TimeSpan.FromSeconds(10));
                    }
                }
                catch
                {
                    throw;
                }
            }
        }
        #endregion

        #region Service Installer *** OBSOLETE ***
        // http://dotnetstep.blogspot.fr/2009/06/install-window-service-without-using.html
        // https://technet.microsoft.com/en-us/library/bb490995.aspx
        [Obsolete]
        private static void install()
        {
            if (IsInstalled()) return;

            string args = string.Format("create {0} start=auto binpath=\"{1}\" ", _serviceName, _servicePath);

            // Install service
            ProcessStartInfo startInfo = new ProcessStartInfo("sc.exe");
            startInfo.Arguments = args;
            ShowMessage("install", args, true);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);

            // Service description
            ProcessStartInfo startInfod = new ProcessStartInfo("sc.exe");
            args = string.Format("description {0}  \"{1}\"", _serviceName);
            ShowMessage("install", args, true);
            startInfod.Arguments = args;
            startInfod.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfod);

            // Start service
            ProcessStartInfo startInfos = new ProcessStartInfo("sc.exe");
            args = string.Format("start {0}", _serviceName);
            ShowMessage("install", args, true);
            startInfos.Arguments = args;
            startInfos.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfos);

            return;
        }

        // https://technet.microsoft.com/en-us/library/bb490995.aspx
        [Obsolete]
        private static void uninstall()
        {
            if (!IsInstalled()) return;

            string args = string.Format("delete {0}", _serviceName);

            ProcessStartInfo startInfo = new ProcessStartInfo("sc.exe");
            startInfo.Arguments = args;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);
            return;
        }
        #endregion

        #region Utils
        private static void TraceInit()
        {
            string displayName = "LessUACRunnerConsole.TraceLevelSwitch";
            string description = "LessUACRunner Console Trace Level Switch";
            string defaultSwitchValue = "0"; // Off

            _traceSwitch =
                new TraceSwitch(displayName, description, defaultSwitchValue);
            Trace.AutoFlush = true;

            Trace.WriteLine("");
            Trace.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            Trace.WriteLine(DateTime.Now.ToString());
            Trace.WriteLine("");
            Trace.WriteLine("TraceSwitch.DisplayName: " + _traceSwitch.DisplayName);
            Trace.WriteLine("TraceSwitch.Description: " + _traceSwitch.Description);
            Trace.WriteLine("TraceSwitch.Level: " + _traceSwitch.Level);
            Trace.WriteLine("");
        }

        private static string FrameWorkVersion()
        {
            string version = Assembly
                     .GetExecutingAssembly()
                     .GetReferencedAssemblies()
                     .Where(x => x.Name == "System.Core").First().Version.ToString();
            return version;
        }

        /// <summary>
        /// Use DataProtectionConfigurationProvider.
        /// </summary>
        /// <param name="encrypt">true=encrypt ; false=decrypt</param>
        /// <param name="fileName"></param>
        public static void EncryptDecryptAppSettings(bool encrypt, string fileName)
        {
            try
            {

                if (encrypt && _section.SectionInformation.IsProtected)
                {
                    SetAndShowError("EncryptDecryptAppSettings", WinService.ErrorCode.ERROR_SectionAlreadyProtected, true);
                    return;
                }
                if (!encrypt && !_section.SectionInformation.IsProtected)
                {
                    SetAndShowError("EncryptDecryptAppSettings", WinService.ErrorCode.ERROR_SectionAlreadyNotProtected, true);
                    return;
                }
                if (encrypt && !_section.SectionInformation.IsProtected)
                {
                    // RsaProtectedConfigurationProvider DataProtectionConfigurationProvider
                    try
                    {
                        _section.SectionInformation.ProtectSection(ConfigurationManager.AppSettings["EncryptionProvider"]);
                    }
                    catch (Exception ex)
                    {
                        ShowMessage("EncryptAppSettings", ex.Message, false);
                        _commandExitCode = -1;
                        return;
                    }
                    ShowMessage("EncryptAppSettings", "encrypted successfully", true);

                }
                if (!encrypt && _section.SectionInformation.IsProtected)
                {
                    try
                    {
                        _section.SectionInformation.UnprotectSection();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage("EncryptAppSettings", ex.Message, false);
                        _commandExitCode = -1;
                        return;
                    }
                    ShowMessage("EncryptAppSettings", "decrypted successfully", true);
                }

                try
                {
                    _section.SectionInformation.ForceSave = true;
                    _configuration.Save(ConfigurationSaveMode.Modified);
                }
                catch (Exception ex)
                {
                    ShowMessage("EncryptAppSettings", ex.Message, false);
                    _commandExitCode = -1;
                }
#if DEBUG
                Process.Start("notepad.exe", _configuration.FilePath);
#endif
            }
            catch (Exception ex)
            {
                ShowMessage("EncryptAppSettings", ex.ToString(), false);
                _commandExitCode = -1;
            }
        }

        private static bool IsEncrypted()
        {
            System.Reflection.Assembly asem = System.Reflection.Assembly.GetExecutingAssembly();
            string fileName = asem.Location;
            if (!File.Exists(fileName))
            {
                SetAndShowError("EncryptDecryptAppSettings", WinService.ErrorCode.ERROR_FileNotFound, true);
                return false;
            }

            return _section.SectionInformation.IsProtected;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true or false</returns>
        private static bool IsAdmin()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Display message (true) or error (false)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type">true = message false = error</param>
        private static void ShowMessage(string source, string message, bool type)
        {
            switch (type)
            {
                case true:
                    System.Console.WriteLine(string.Format("[{0}] {1}", source, message));
                    break;
                case false:
                    System.Console.WriteLine(string.Format("[{0}] ERROR: {1}", source, message));
                    break;
                default:
                    break;
            }
        }

        private static void ShowMessage(string message)
        {
            System.Console.WriteLine(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="type">true = message false = error</param>
        private static void SetAndShowError(string source, WinService.ErrorCode errorCode, bool type)
        {
            _commandExitCode = (int)errorCode;
            ShowMessage(source, WinService.LessError.GetEnumDescription(errorCode), type);
        }

        #endregion
    }
}
