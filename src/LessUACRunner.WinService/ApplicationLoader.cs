#define NETFX_35
#undef NETFX_40

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Security.Principal;
using System.Management;
using System.Threading;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Web.Script.Serialization;

namespace LessUACRunner.WinService
{
    /// <summary>
    /// Class that allows running applications with full admin rights. In
    /// addition the application launched will bypass the Windows UAC prompt.
    /// </summary>
    public class ApplicationLoader
    {
        #region Types
        private class ProcessReturn
        {
            public string StdError = null;
            public string StdOut = null;
            public int ExitCode = 0;
            public string ElapsedTime = null;
        }
        #endregion

        public ApplicationLoader(string applicationName, bool console, string pipeName)
        {
            TraceInit();

            ApplicationName = applicationName;
            IsConsole = console;
            PipeName = pipeName;
        }

        #region Private fields
        private string ApplicationName;
        private bool IsConsole;
        private string PipeName;
        private  Stopwatch stopWatch = new Stopwatch();
        private string clientOutput = string.Empty;
        private string clientErrorOutput = string.Empty;
        private  TraceSwitch _traceSwitch;
        #endregion

        #region MAIN

        /// <summary>
        /// Launches the given application with full admin rights, and in addition bypasses the Vista UAC prompt
        /// http://www.vbforums.com/showthread.php?618110-Redirect-Process-Input-Output-with-Windows-API&p=3821573#post3821573
        /// </summary>
        public void StartProcessAndBypassUACThread()
        {
            stopWatch.Start();

            Win32API.PROCESS_INFORMATION procInfo;
            IntPtr CurrentOutputHandle = IntPtr.Zero;
            IntPtr CurrentErrorOutputHandle = IntPtr.Zero;
            uint lpExitCode;

            string applicationName = ApplicationName;
            bool console = IsConsole;
            string pipeName = PipeName;

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread"));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: {0}", applicationName));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: DNP name = {0}", pipeName));
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: Console = {0}", console));

            #region If console 0
            //StopMonitoring = false;
            IntPtr TmpReadOutputHandle = IntPtr.Zero;
            IntPtr TmpReadErrorOutputHandle = IntPtr.Zero;
            #endregion

            uint winlogonPid = 0;
            IntPtr hUserTokenDup = IntPtr.Zero, hPToken = IntPtr.Zero, hProcess = IntPtr.Zero;
            procInfo = new Win32API.PROCESS_INFORMATION();

            // ** 0 ** Init STARTUPINFO
            // By default CreateProcessAsUser creates a process on a non-interactive window station, meaning
            // the window station has a desktop that is invisible and the process is incapable of receiving
            // user input. To remedy this we set the lpDesktop parameter to indicate we want to enable user 
            // interaction with the new process.
            Win32API.STARTUPINFO startInfo = new Win32API.STARTUPINFO();
            startInfo.cb = (int)Marshal.SizeOf(startInfo);

            #region If console 0a
            if (console)
            {
                startInfo.dwFlags = Win32API.STARTF_USESTDHANDLES;
            }
            #endregion

            startInfo.lpDesktop = @"winsta0\default"; // interactive window station parameter; basically this indicates that the process created can display a GUI on the desktop  

            // ** 1 ** obtain the currently active session id; every logged on user in the system has a unique session id
            uint dwSessionId = Win32API.WTSGetActiveConsoleSessionId();
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: WTSGetActiveConsoleSessionId=" + dwSessionId.ToString());

            // ** 1a ** GetUserProfile for logged on user
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: GetUserProfile");
            Win32API.PROFILEINFO profileInfo = new Win32API.PROFILEINFO();
            bool resultGetUserProfile = GetUserProfile(dwSessionId, ref profileInfo);
            if (!resultGetUserProfile)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: GetUserProfile: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return;
            }

            // ** 1b ** Only to show PROFILEINFO.hProfile handle usage.
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: AccessHkcuRegistry(winlogon)");
            
            #if NETFX_40
            // TODO: Profile
            AccessHkuRegistry(profileInfo.hProfile);
            #endif
            
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: GetProcessesByName(winlogon)");
            // ** 2 ** obtain the process id of the winlogon process that is running within the currently active session GetProcessesByName
            Process[] processes = Process.GetProcessesByName("winlogon");
            foreach (Process p in processes)
            {
                if ((uint)p.SessionId == dwSessionId)
                {
                    winlogonPid = (uint)p.Id;
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: winlogonPid: {0}", winlogonPid.ToString()));
                }
            }

            // ** 3 ** obtain a handle to the winlogon process
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: OpenProcess");
            hProcess = Win32API.OpenProcess(Win32API.MAXIMUM_ALLOWED, false, winlogonPid);

            // ** 4 ** obtain a handle to the access token of the winlogon process
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: OpenProcessToken");
            if (!Win32API.OpenProcessToken(hProcess, Win32API.TOKEN_DUPLICATE, ref hPToken))
            {
                Win32API.CloseHandle(hProcess);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: OpenProcessToken: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return;
            }

            // ** 4a ** Security attribute structure used in DuplicateTokenEx and CreateProcessAsUser
            // I would prefer to not have to use a security attribute variable and to just 
            // simply pass null and inherit (by default) the security attributes
            // of the existing token. However, in C# structures are value types and therefore
            // cannot be assigned the null value.
            Win32API.SECURITY_ATTRIBUTES securityAttributes = new Win32API.SECURITY_ATTRIBUTES();
            securityAttributes.Length = Marshal.SizeOf(securityAttributes);
            
            #region If console 1
            if (console)
            {
                securityAttributes.bInheritHandle = true;
                IntPtr attr = Marshal.AllocHGlobal(Marshal.SizeOf(securityAttributes));
                Marshal.StructureToPtr(securityAttributes, attr, true);

                // Create pipe for console output and store the "read end" handle in TmpReadOutputHandle and
                // the "write end" handle is given to our startup parameters object so that the process we create
                // can write to it instead of writing to its normal output (the screen)
                // TmpReadOutputHandle = handle to read the pipe ; StartInfo.hStdOutput = handle to write to the pipe
                if (!Win32API.CreatePipe(out TmpReadOutputHandle, out startInfo.hStdOutput, attr, 0))
                {
                    Debug.WriteLine("Error CreatePipe stdout");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,"StartProcessAndBypassUACThread: Error CreatePipe stdout");
                }

                // Ensure the write handle to the pipe for STDOUT is not inherited. 
                Win32API.SetHandleInformation(TmpReadOutputHandle, Win32API.HANDLE_FLAGS.INHERIT, 0);

                // Create pipe for console error output and store the "read end" handle in TmpReadErrorOutputHandle and
                // the "write end" handle is given to our startup parameters object so that the process we create
                // can write to it instead of writing to its normal output (the screen)
                // TmpReadErrorOutputHandle = handle to read the pipe ; StartInfo.hStdError = handle to write to the pipe
                if (!Win32API.CreatePipe(out TmpReadErrorOutputHandle, out startInfo.hStdError, attr, 0))
                {
                    Debug.WriteLine("Error CreatePipe stderr");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,"StartProcessAndBypassUACThread: Error CreatePipe stderr");
                }

                // Ensure the write handle to the pipe for STDERR is not inherited. 
                Win32API.SetHandleInformation(TmpReadErrorOutputHandle, Win32API.HANDLE_FLAGS.INHERIT, 0);

                // Get process handle
                IntPtr processHandle = Process.GetCurrentProcess().Handle;

                // Duplicate the "read end" handle for the output pipe and make the duplicated handle non inheritable
                // CurrentOutputHandle = handle to read the pipe from parent
                Win32API.DuplicateHandle(
                    processHandle,
                    TmpReadOutputHandle,
                    processHandle,
                    out CurrentOutputHandle,
                    0,
                    false,
                    Win32API.DUPLICATE_SAME_ACCESS);
                    
                // Duplicate the "read end" handle for the error output pipe and make the duplicated handle non inheritable
                // CurrentOutputHandle = handle to read the pipe from parent
                Win32API.DuplicateHandle(
                    processHandle,
                    TmpReadErrorOutputHandle,
                    processHandle,
                    out CurrentErrorOutputHandle,
                    0,
                    false,
                    Win32API.DUPLICATE_SAME_ACCESS);

                // Close inheritable copies of the handles we do not want to be inherited.
                Win32API.CloseHandle(TmpReadOutputHandle);
                Win32API.CloseHandle(TmpReadErrorOutputHandle);
            }
            #endregion

            // ** 5 ** copy the access token of the winlogon process; the newly created token will be a primary token
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: DuplicateTokenEx");
            if (!Win32API.DuplicateTokenEx(hPToken, Win32API.MAXIMUM_ALLOWED, ref securityAttributes, (int)Win32API.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)Win32API.TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
            {
                Win32API.CloseHandle(hProcess);
                Win32API.CloseHandle(hPToken);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: DuplicateTokenEx: {0}", Marshal.GetLastWin32Error().ToString()));
                return;
            }

            // ** 5a ** flags that specify the priority and creation method of the process
            int dwCreationFlags = Win32API.NORMAL_PRIORITY_CLASS;

            #region If console 1a
            if (console)
            {
                dwCreationFlags |= Win32API.CREATE_NO_WINDOW;
            }
            else
            {
                dwCreationFlags |= Win32API.CREATE_NEW_CONSOLE;
            }
            #endregion            
            
            // ** 5b ** GetUserEnvironement
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: GetUserEnvironement");
            IntPtr lpEnvironment = GetUserEnvironement(dwSessionId);
            if (lpEnvironment != IntPtr.Zero)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: CREATE_UNICODE_ENVIRONMENT");
                dwCreationFlags |= Win32API.CREATE_UNICODE_ENVIRONMENT;
            }

            #region If console 1b
            if (console)
            {
                dwCreationFlags |= Win32API.NORMAL_PRIORITY_PROCESS;
            }

            #endregion

            // ** 5c ** SetTokenInformation
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: SetTokenInformation");
            
            //ONLY if requiring UIAccess!
            // http://stackoverflow.com/questions/23209760/createprocessasuser-fails-with-error-elevation-required-for-a-process-with-uiacc
            uint dwUIAccess = 1;
            bool resultSetToken = Win32API.SetTokenInformation(hUserTokenDup, Win32API.TOKEN_INFORMATION_CLASS.TokenUIAccess, ref dwUIAccess, (UInt32)Marshal.SizeOf(dwUIAccess));
            if (!resultSetToken)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: SetTokenInformation: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
            }
            
            Trace.WriteLineIf(_traceSwitch.TraceVerbose, "StartProcessAndBypassUACThread: CreateProcessAsUser");
            string commandLine = applicationName;
            string fileToExecute = null;
            //string curDir = "C:\\Pg\\LessUACRunner";
            string curDir = System.AppDomain.CurrentDomain.BaseDirectory;
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: CurrentDirectory: " + curDir);
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: file to execute: " + fileToExecute);
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: command line: " + commandLine);

            // ** 6 ** create a new process in the current user's logon session
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms682429%28v=vs.85%29.aspx
            bool result = Win32API.CreateProcessAsUser(hUserTokenDup,       // client's access token
                                            fileToExecute,                  // file to execute
                                            commandLine,                    // command line
                                            ref securityAttributes,         // pointer to process SECURITY_ATTRIBUTES
                                            ref securityAttributes,         // pointer to thread SECURITY_ATTRIBUTES
                                            true,                           // handles are not inheritable
                                            dwCreationFlags,                // creation flags
                                            lpEnvironment,                  // pointer to new environment block 
                                            curDir,                         // name of current directory 
                                            ref startInfo,                  // pointer to STARTUPINFO structure
                                            out procInfo                    // receives information about new process
                                            );
            if (!result)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: CreateProcessAsUser: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return;
            }

            #region If console 2
            if (console)
            {
                // Get PROCESS handle
                IntPtr CurrentProcessHandle = procInfo.hProcess;

                // RUN OUTPUTWATCHER THREADS
                Thread OutputWatcherThread = new Thread(GetProcessOutPutThread);
                Thread ErrorOutputWatcherThread = new Thread(GetProcessErrorOutPutThread);

                object outputWatcherParams = new object[3] { CurrentOutputHandle, ErrorOutputWatcherThread, CurrentProcessHandle };
                object errorOutputWatcherParams = new object[3] { CurrentErrorOutputHandle, OutputWatcherThread, CurrentProcessHandle };

                // Start GetProcessOutPutThread
                OutputWatcherThread.IsBackground = true;
                OutputWatcherThread.Start(outputWatcherParams);
                Debug.WriteLine("OutputWatcherThread={0}", OutputWatcherThread.IsAlive.ToString());
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: OutputWatcherThread={0}", OutputWatcherThread.IsAlive.ToString()));

                // Start ErrorOutputWatcherThread
                ErrorOutputWatcherThread.IsBackground = true;
                ErrorOutputWatcherThread.Start(errorOutputWatcherParams);
                Debug.WriteLine("ErrorOutputWatcherThread={0}", ErrorOutputWatcherThread.IsAlive.ToString());
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: ErrorOutputWatcherThread={0}", ErrorOutputWatcherThread.IsAlive.ToString()));

                // WAIT OutputWatcherThread  AND ErrorOutputWatcherThread END
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: Waiting for ErrorOutputWatcherThread end"));
                ErrorOutputWatcherThread.Join();
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: ErrorOutputWatcherThread={0}", ErrorOutputWatcherThread.IsAlive.ToString()));

                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: Waiting for OutputWatcherThread end"));
                OutputWatcherThread.Join();
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: OutputWatcherThread={0}", OutputWatcherThread.IsAlive.ToString()));

                // TODO: WAIT PROCESS END AND RETRIEVE EXITCODE
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: Waiting for PROCESS end"));
                IntPtr[] WaitOnHandles = new IntPtr[1];
                WaitOnHandles[0] = CurrentProcessHandle;
                Win32API.WaitForMultipleObjects(1, WaitOnHandles, false, 100000);
                Win32API.GetExitCodeProcess(procInfo.hProcess, out lpExitCode);

                // WRITE DATA TO IN DATA NAMED PIPE (DNP)
                WriteCommandDataViaDNP( pipeName, lpExitCode);

            }
            #endregion            
            
            // DESTROY ALL BEFORE TERMINATE
            
            // ** 6a ** UnloadUserProfile
            //// https://msdn.microsoft.com/en-us/library/windows/desktop/bb762282%28v=vs.85%29.aspx
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: UnloadUserProfile"));
            bool resultUnLoadUserProfile = Win32API.UnloadUserProfile(hUserTokenDup, profileInfo.hProfile);
            if (!resultUnLoadUserProfile)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: UnloadUserProfile: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
            }

            // ** 6b ** DestroyEnvironmentBlock
            if (lpEnvironment != IntPtr.Zero)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "StartProcessAndBypassUACThread: DestroyEnvironmentBlock");
                bool resultDestroyEnvironmentBlock = Win32API.DestroyEnvironmentBlock(lpEnvironment);
                if (!resultDestroyEnvironmentBlock)
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: DestroyEnvironmentBlock: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                }
            }

            // ** 7 ** invalidate the handles
            Win32API.CloseHandle(procInfo.hThread);
            Win32API.CloseHandle(hProcess);
            Win32API.CloseHandle(hPToken);
            Win32API.CloseHandle(hUserTokenDup);

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("StartProcessAndBypassUACThread: CreateProcessAsUser result: {0}", result.ToString()));
            return; // END OF StartProcessAndBypassUACThread
        }

        #region Console
        private void WriteCommandDataViaDNP( string pipeName, uint lpExitCode)
        {
            ProcessReturn processReturn = new ProcessReturn();

            // Create Data Named Pipe (DNP)
            SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule pr = new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow);

            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(pr);

            using (NamedPipeServerStream pipeServer =
                  new NamedPipeServerStream(
                      pipeName,
                      PipeDirection.Out,
                      NamedPipeServerStream.MaxAllowedServerInstances,
                      PipeTransmissionMode.Message,
                      PipeOptions.None,
                      4096,
                      4096,
                      ps,
                      HandleInheritability.None))
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("WriteCommandDataViaDNP: DNP Created: {0}", pipeName));
                Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("WriteCommandDataViaDNP: Waiting on DNP for client connection... at {0}", DateTime.Now.ToString()));
                try
                {
                    pipeServer.WaitForConnection();

                }
                catch (Exception e)
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("WriteCommandDataViaDNP: LessUACRunnerThread.WaitForConnection() exception:  {0}", e.Message));
                }

                try
                {
                    stopWatch.Stop();
                    // Get the elapsed time as a TimeSpan value.
                    TimeSpan ts = stopWatch.Elapsed;
                    // Format and display the TimeSpan value.
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("StartProcessAndBypassUACThread: RunTime GetProcessOutPutThread = {0}", elapsedTime));


                    using (StreamWriter sw = new StreamWriter(pipeServer))
                    {
                        // Fill json object
                        processReturn.StdOut = clientOutput;
                        processReturn.StdError = clientErrorOutput;
                        processReturn.ExitCode = (int)lpExitCode;
                        processReturn.ElapsedTime = elapsedTime;

                        var json = new JavaScriptSerializer().Serialize(processReturn);

                        Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("WriteCommandDataViaDNP: Writing to DNP in JSON format: \n{0}", json).Replace("\n", Environment.NewLine));

                        sw.AutoFlush = true;
                        sw.Write(json);
                        clientOutput = string.Empty;
                        clientErrorOutput = string.Empty;
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose, string.Format("WriteCommandDataViaDNP: LessUACRunnerThread.Write exception:  {0}", e.Message));
                }

            }

        }
        private void GetProcessErrorOutPutThread(object data)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread"));

            Array argArray = new object[3];
            argArray = (Array)data;
            IntPtr CurrentErrorOutputHandle = (IntPtr)argArray.GetValue(0);
            Thread OutputWatcherThread =  (Thread)argArray.GetValue(1);
            IntPtr CurrentProcessHandle = (IntPtr)argArray.GetValue(2);

            ManualResetEvent ThreadSignal = new ManualResetEvent(false);
            bool StopMonitoring = false;

            int i = 0;

            //Loop that stops the thread from ending after it has read all currently available output
            do
            {
                Debug.WriteLine("ENTER: Loop that stops the thread");
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: ENTER: Loop that stops the thread");
                //Loop that reads from the output pipe until there is no more data to be read
                do
                {
                    Debug.WriteLine("ENTER: Loop that reads from the output pipe");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: ENTER: Loop that reads from the output pipe");

                    System.Threading.Thread.Sleep(700);

                    byte[] Buffer = new byte[4096];
                    uint aPeekedBytes = 0;
                    uint aLeftBytes = 0;
                    uint BytesRead = 0;
                    uint BytesAvailable = 0;

                    bool PeekResult = Win32API.PeekNamedPipe(CurrentErrorOutputHandle, null, 0, ref aPeekedBytes, ref BytesAvailable, ref aLeftBytes);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: BytesAvailable: {0}", BytesAvailable.ToString()));

                    if (PeekResult)
                    {
                        if (BytesAvailable > 0)
                        {
                            if (Win32API.ReadFile(CurrentErrorOutputHandle, Buffer, (uint)Buffer.Length, out BytesRead, IntPtr.Zero))
                            {
                                i = i + 1;
                                Console.WriteLine("Val i = {0}", i);
                                Debug.WriteLine("Val i = {0}", i.ToString());

                                // Store Buffer in the string asString 
                                int len = Array.IndexOf(Buffer, (byte)0);
                                string asString = string.Empty;
                                asString = Encoding.UTF8.GetString(Buffer, 0, len);
                                Debug.WriteLine(string.Format("GetProcessErrorOutPutThread: asString = {0}", asString));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: asString = {0}", asString));

                                // Concat in global final var clientErrorOutput
                                clientErrorOutput += asString;
                            }
                            else
                            {
                                Debug.WriteLine(string.Format("There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                break;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No bytes available");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: No bytes available");
                            break;
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        break;
                    }

                } while (true); //Back to the start to check if any more data is available to be read

                ThreadSignal.Reset();
                IntPtr[] WaitOnHandles = new IntPtr[2];
                WaitOnHandles[0] = CurrentProcessHandle;
                WaitOnHandles[1] = ThreadSignal.SafeWaitHandle.DangerousGetHandle();
                Debug.WriteLine("Waiting for signal or process termination");
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Waiting for signal or process termination");
                uint ContinueReason = Win32API.WaitForMultipleObjects(2, WaitOnHandles, false, 10000);
                Debug.WriteLine("ContinueReason = {0}", ContinueReason.ToString());
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: ContinueReason = {0}", ContinueReason.ToString()));
                if (ContinueReason == 0)
                {
                    Debug.WriteLine("Continue reason = process terminated");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Continue reason = process terminated");
                    // ensure no more data in the pipe when process end
                    byte[] Buffer = new byte[4096];
                    uint aPeekedBytes = 0;
                    uint aLeftBytes = 0;
                    uint BytesRead = 0;
                    uint BytesAvailable = 0;

                    bool PeekResult = Win32API.PeekNamedPipe(CurrentErrorOutputHandle, null, 0, ref aPeekedBytes, ref BytesAvailable, ref aLeftBytes);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: BytesAvailable: {0}", BytesAvailable.ToString()));

                    if (PeekResult)
                    {
                        Console.WriteLine("Ensure no more data in the pipe when process end");

                        if (BytesAvailable > 0)
                        {
                            if (Win32API.ReadFile(CurrentErrorOutputHandle, Buffer, (uint)Buffer.Length, out BytesRead, IntPtr.Zero))
                            {
                                i = i + 1;
                                Console.WriteLine("Val i = {0}", i);
                                Debug.WriteLine("Val i = {0}", i.ToString());

                                // Store Buffer in the string asString 
                                int len = Array.IndexOf(Buffer, (byte)0);
                                string asString = string.Empty;
                                asString = Encoding.UTF8.GetString(Buffer, 0, len);
                                Debug.WriteLine(string.Format("GetProcessErrorOutPutThread: asString2 = {0}", asString));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: asString2 = {0}", asString));

                                // Concat in global final var clientErrorOutput
                                clientErrorOutput += asString;
                            }
                            else
                            {
                                Debug.WriteLine(string.Format("There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No bytes available 2");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: No bytes available 2");
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                    }

                    // END
                    //if (!OutputWatcherThread.IsAlive) DestroyHandles(CurrentProcessHandle);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: terminates error thread OutputWatcherThread.IsAlive: {0}", OutputWatcherThread.IsAlive));
                    break; //terminates this thread as there is no more work to do once we exit this loop
                }
                else
                {
                    if (ContinueReason == 1)
                    {
                        Debug.WriteLine("Continue reason = signal received");
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Continue reason = signal received");
                        if (StopMonitoring)
                        {
                            Debug.WriteLine("Output monitoring thread terminating");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Output monitoring thread terminating");
                            //if (!OutputWatcherThread.IsAlive) DestroyHandles(CurrentProcessHandle);
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: terminates error thread OutputWatcherThread.IsAlive: {0}", OutputWatcherThread.IsAlive));
                            break; //terminates this thread
                        }
                        else
                        {
                            //If StopMonitoring is not true then we go back to the start of the loop (which then starts 
                            //the read loop again as well) to check for more output
                            continue;
                        }
                    }
                    else
                    {
                        if (ContinueReason == Win32API.WAIT_TIMEOUT)
                        {
                            Debug.WriteLine("Continue reason = timed out");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Continue reason = timed out");
                        }
                        if (ContinueReason == Win32API.WAIT_FAILED)
                        {
                            Debug.WriteLine(string.Format("WaitForMultipleObjects failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessErrorOutPutThread: WaitForMultipleObjects failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        }
                    }
                }
            } while (true);
            Debug.WriteLine("Background thread terminating");
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessErrorOutPutThread: Background thread terminating");
        }
        private void GetProcessOutPutThread(object data)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread"));

            Array argArray = new object[3];
            argArray = (Array)data;
            IntPtr CurrentOutputHandle = (IntPtr)argArray.GetValue(0);
            Thread ErrorOutputWatcherThread = (Thread)argArray.GetValue(1);
            IntPtr CurrentProcessHandle = (IntPtr)argArray.GetValue(2);

            ManualResetEvent ThreadSignal = new ManualResetEvent(false);
            bool StopMonitoring = false;

            int i = 0;

            //Loop that stops the thread from ending after it has read all currently available output
            do
            {
                Debug.WriteLine("ENTER: Loop that stops the thread");
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: ENTER: Loop that stops the thread");
                //Loop that reads from the output pipe until there is no more data to be read
                do
                {
                    Debug.WriteLine("ENTER: Loop that reads from the output pipe");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: ENTER: Loop that reads from the output pipe");

                    System.Threading.Thread.Sleep(700);

                    byte[] Buffer = new byte[4096];
                    uint aPeekedBytes = 0;
                    uint aLeftBytes = 0;
                    uint BytesRead = 0;
                    uint BytesAvailable = 0;

                    bool PeekResult = Win32API.PeekNamedPipe(CurrentOutputHandle, null, 0, ref aPeekedBytes, ref BytesAvailable, ref aLeftBytes);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: BytesAvailable: {0}", BytesAvailable.ToString()));

                    if (PeekResult)
                    {
                        if (BytesAvailable > 0)
                        {
                            if (Win32API.ReadFile(CurrentOutputHandle, Buffer, (uint)Buffer.Length, out BytesRead, IntPtr.Zero))
                            {
                                i = i + 1;
                                Console.WriteLine("Val i = {0}", i);
                                Debug.WriteLine("Val i = {0}", i.ToString());

                                // Store Buffer in the string asString 
                                int len = Array.IndexOf(Buffer, (byte)0);
                                string asString = string.Empty;
                                asString = Encoding.UTF8.GetString(Buffer, 0, len);
                                Debug.WriteLine(string.Format("GetProcessOutPutThread: asString = {0}", asString));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: asString = {0}", asString));

                                // Concat in global final var clientOutput
                                clientOutput += asString;
                            }
                            else
                            {
                                Debug.WriteLine(string.Format("There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                break;
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No bytes available");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: No bytes available");
                            break;
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        break;
                    }

                } while (true); //Back to the start to check if any more data is available to be read

                ThreadSignal.Reset();
                IntPtr[] WaitOnHandles = new IntPtr[2];
                WaitOnHandles[0] = CurrentProcessHandle;
                WaitOnHandles[1] = ThreadSignal.SafeWaitHandle.DangerousGetHandle();
                Debug.WriteLine("Waiting for signal or process termination");
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Waiting for signal or process termination");
                uint ContinueReason = Win32API.WaitForMultipleObjects(2, WaitOnHandles, false, 10000);
                Debug.WriteLine("ContinueReason = {0}", ContinueReason.ToString());
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: ContinueReason = {0}", ContinueReason.ToString()));
                if (ContinueReason == 0)
                {
                    Debug.WriteLine("Continue reason = process terminated");
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Continue reason = process terminated");
                    // ensure no more data in the pipe when process end
                    byte[] Buffer = new byte[4096];
                    uint aPeekedBytes = 0;
                    uint aLeftBytes = 0;
                    uint BytesRead = 0;
                    uint BytesAvailable = 0;

                    bool PeekResult = Win32API.PeekNamedPipe(CurrentOutputHandle, null, 0, ref aPeekedBytes, ref BytesAvailable, ref aLeftBytes);
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: BytesAvailable: {0}", BytesAvailable.ToString()));

                    if (PeekResult)
                    {
                        Console.WriteLine("Ensure no more data in the pipe when process end");

                        if (BytesAvailable > 0)
                        {
                            if (Win32API.ReadFile(CurrentOutputHandle, Buffer, (uint)Buffer.Length, out BytesRead, IntPtr.Zero))
                            {
                                i = i + 1;
                                Console.WriteLine("Val i = {0}", i);
                                Debug.WriteLine("Val i = {0}", i.ToString());

                                // Store Buffer in the string asString 
                                int len = Array.IndexOf(Buffer, (byte)0);
                                string asString = string.Empty;
                                asString = Encoding.UTF8.GetString(Buffer, 0, len);
                                Debug.WriteLine(string.Format("GetProcessOutPutThread: asString2 = {0}", asString));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: asString2 = {0}", asString));

                                // Concat in global final var clientOutput
                                clientOutput += asString;
                            }
                            else
                            {
                                Debug.WriteLine(string.Format("There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: There was a problem reading the process output. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No bytes available 2");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: No bytes available 2");
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: Peek failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                    }

                    // END
                    //if (!ErrorOutputWatcherThread.IsAlive) DestroyHandles(CurrentProcessHandle);
                    //DestroyHandles();
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: terminates output thread ErrorOutputWatcherThread.IsAlive: {0}", ErrorOutputWatcherThread.IsAlive));
                    break; //terminates this thread as there is no more work to do once we exit this loop
                }
                else
                {
                    if (ContinueReason == 1)
                    {
                        Debug.WriteLine("Continue reason = signal received");
                        Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Continue reason = signal received");
                        if (StopMonitoring)
                        {
                            Debug.WriteLine("Output monitoring thread terminating");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Output monitoring thread terminating");
                            //if (!ErrorOutputWatcherThread.IsAlive) DestroyHandles(CurrentProcessHandle);
                            //DestroyHandles();
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: terminates output thread ErrorOutputWatcherThread.IsAlive: {0}", ErrorOutputWatcherThread.IsAlive));
                            break; //terminates this thread
                        }
                        else
                        {
                            //If StopMonitoring is not true then we go back to the start of the loop (which then starts 
                            //the read loop again as well) to check for more output
                            continue;
                        }
                    }
                    else
                    {
                        if (ContinueReason == Win32API.WAIT_TIMEOUT)
                        {
                            Debug.WriteLine("Continue reason = timed out");
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Continue reason = timed out");
                        }
                        if (ContinueReason == Win32API.WAIT_FAILED)
                        {
                            Debug.WriteLine(string.Format("WaitForMultipleObjects failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetProcessOutPutThread: WaitForMultipleObjects failed. The last error reported was: {0}", new System.ComponentModel.Win32Exception().Message));
                        }
                    }
                }
            } while (true);
            Debug.WriteLine("Background thread terminating");
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetProcessOutPutThread: Background thread terminating");
        }
        #endregion

        #region GUI
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dwSessionId"></param>
        /// <param name="profileInfo"></param>
        /// <returns></returns>
        private  bool GetUserProfile(uint dwSessionId, ref Win32API.PROFILEINFO profileInfo)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserProfile");

            IntPtr userToken = IntPtr.Zero;

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserProfile: GetUsernameBySessionId");
            string username = GetUsernameBySessionId(dwSessionId);

            // https://msdn.microsoft.com/en-us/library/windows/desktop/bb762281%28v=vs.85%29.aspx            
            profileInfo.dwSize = Marshal.SizeOf(profileInfo);
            profileInfo.lpUserName = username;
            profileInfo.dwFlags = 1;

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserProfile: WTSQueryUserToken");
            bool resultWTSQueryUserToken = Win32API.WTSQueryUserToken(dwSessionId, out userToken);
            if (!resultWTSQueryUserToken)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserProfile: WTSQueryUserToken: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return false;
            }
            // TODO: Profile
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserProfile: LoadUserProfile");
            bool resultLoadUserProfile = Win32API.LoadUserProfile(userToken, ref profileInfo);
            if (!resultLoadUserProfile)
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserProfile: LoadUserProfile: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return false;
            }

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserProfile: UserName: {0}", profileInfo.lpUserName));

            // Invalidate handles.
            //CloseHandle(userToken);
            
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserProfile: GetUserSID");
            string sid = GetUserSID(username);
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserProfile: UserName: {0} SID: {1}", username, sid));

            return true;

        }
        /// <summary>
        /// Get logged user's variables environement InPtr using explorer process access token.
        /// </summary>
        /// <returns>InPtr to user variables environement</returns>
        private  IntPtr GetUserEnvironement(uint dwSessionId)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement");

            IntPtr hPToken = IntPtr.Zero, hUserTokenDup = IntPtr.Zero;

            Win32API.SECURITY_ATTRIBUTES sa = new Win32API.SECURITY_ATTRIBUTES();
            sa.Length = Marshal.SizeOf(sa);

            Win32API.WTSQueryUserToken(dwSessionId, out hPToken);///////////////        
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: DuplicateTokenEx");
            // copy the access token of the winlogon process; the newly created token will be a primary token
            if (!Win32API.DuplicateTokenEx(hPToken, Win32API.MAXIMUM_ALLOWED, ref sa, (int)Win32API.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)Win32API.TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
            {
                //CloseHandle(hProcess);
                Win32API.CloseHandle(hPToken);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: DuplicateTokenEx: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return IntPtr.Zero;
            }

            IntPtr lpEnvironment = IntPtr.Zero;
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: CreateEnvironmentBlock");
            bool resultEnv = Win32API.CreateEnvironmentBlock(ref lpEnvironment, hUserTokenDup, false);
            if (resultEnv != true)
            {
                lpEnvironment = IntPtr.Zero;
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: CreateEnvironmentBlock: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
            }
            else
            {
                System.Collections.Generic.IEnumerable<string> str1 = ExtractMultiString(lpEnvironment);
                foreach (var item in str1)
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: CreateEnvironmentBlock Environment: {0}", item.ToString()));
                }
            }
            // Invalidate handles.
            Win32API.CloseHandle(hPToken);

            return lpEnvironment;
        }
        #endregion

        #endregion

        #region Utils
        private void TraceInit()
        {
            string displayName = "LessUACRunnerService.TraceLevelSwitch";
            string description = "LessUACRunner Service Trace Level Switch";
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
        /// <summary>
        ///
        /// </summary>
        /// <param name="dwSessionId"></param>
        /// <returns></returns>
        private  string GetUsernameBySessionId(uint dwSessionId)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUsernameBySessionId");

            IntPtr buffer;
            int strLen;
            string username = string.Empty;

            if (Win32API.WTSQuerySessionInformation(IntPtr.Zero, dwSessionId, Win32API.WtsInfoClass.WTSUserName, out buffer, out strLen) && strLen > 1)
            {
                username = Marshal.PtrToStringAnsi(buffer);
            }
            else
            {
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUsernameBySessionId: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
            }
            return username;
        }
        
        /// <summary>
        ///
        /// </summary>
        /// <param name="username"></param>
        /// <returns>SID</returns>
        private  string GetUserSID(string username)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserSID");

            var account = new NTAccount(username);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            return sid.ToString();
        }
        private static string GetLastWin32ErrorMessage()
        {
            string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return errorMessage;
        }
        
        /// <summary>
        /// Extract an array of null-terminated Unicode strings. The list ends with two nulls (\0\0).
        /// Return a collection of array's elements of type string.
        /// http://www.scriptscoop.net/t/fa1ad16e580c/c-decoding-intptr-to-multistring.html
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns>IEnumerable<string></returns>
        private static System.Collections.Generic.IEnumerable<string> ExtractMultiString(IntPtr ptr)
        {
            while (true)
            {
                string str = Marshal.PtrToStringUni(ptr);
                if (str.Length == 0)
                    break;
                yield return str;
                ptr = new IntPtr(ptr.ToInt64() + (str.Length + 1 /* char \0 */) * sizeof(char));
            }
        }
        #endregion

        #if NETFX_40
        #region For Info
        /// <summary>
        /// Read write the logged user registry HKU key from service using hProfile created by LoadUserProfile.
        /// TODO: Profile
        /// </summary>
        /// <param name="hkcuHandle"></param>
        private static void AccessHkuRegistry(IntPtr hkuHandle)
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "AccessHkuRegistry");

            // Access registry HKU
            using (SafeRegistryHandle safeHandle =
                      new SafeRegistryHandle(hkuHandle, true))
            {
                using (RegistryKey userHKU = RegistryKey.FromHandle(safeHandle))
                {
                    // Unum all sub keys under tempuser's HKU
                    String[] keys = userHKU.GetSubKeyNames();

                    // Create a new sub key under tempuser's HKU
                    using (RegistryKey tempKeyByWayne =
                           userHKU.CreateSubKey("TempKeyByWayne"))
                    {
                        // Create a new String value under created TempKeyByWayne subkey
                        tempKeyByWayne.SetValue("StrType",
                             "TempContent", RegistryValueKind.String);

                        // Read the value
                        using (RegistryKey regKey =
                                 userHKU.OpenSubKey("TempKeyByWayne"))
                        {
                            String valueContent = regKey.GetValue("StrType") as String;
                            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("AccessHkuRegistry: TempKeyByWayne Value={0}", valueContent));

                        }

                        // Delete created TempKeyByWayne subkey
                        //tempUserHKU.DeleteSubKey("TempKeyByWayne");
                        tempKeyByWayne.Close();
                    }
                }
            }
        }
        #endregion
        #endif
        
        #region *** OBSOLETE ***
        /// <summary>
        /// OBSOLETE !!!
        /// </summary>
        /// <returns></returns>
        [Obsolete]
        private  string GetExplorerUser()
        {
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetExplorerUser");

            var query = new ObjectQuery(
                "SELECT * FROM Win32_Process WHERE Name = 'explorer.exe'");

            var explorerProcesses = new ManagementObjectSearcher(query).Get();

            foreach (ManagementObject mo in explorerProcesses)
            {
                string[] ownerInfo = new string[2];
                mo.InvokeMethod("GetOwner", (object[])ownerInfo);

                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetExplorerUser: {0}", String.Concat(ownerInfo[1], @"\", ownerInfo[0])));

                return ownerInfo[0];

            }

            return string.Empty;
        }

        /// <summary>
        /// OBSOLETE !!!
        /// </summary>
        /// <param name="dwSessionId"></param>
        /// <returns></returns>
        [Obsolete]
        private  IntPtr GetUserEnvironementOld(uint dwSessionId)
        {
            uint explorerPid = 0;

            IntPtr hPToken = IntPtr.Zero, hProcess = IntPtr.Zero, hUserTokenDup = IntPtr.Zero;

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: GetProcessesByName(explorer)");
            // obtain the process id of the explorer process that is running within the currently active session
            Process[] processesx = Process.GetProcessesByName("explorer");
            foreach (Process p in processesx)
            {
                if ((uint)p.SessionId == dwSessionId)
                {
                    explorerPid = (uint)p.Id;
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: explorerPid= {0}", explorerPid.ToString()));
                }
            }

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: OpenProcess");
            // obtain a handle to the explorer process
            hProcess = Win32API.OpenProcess(Win32API.MAXIMUM_ALLOWED, false, explorerPid);

            // obtain a handle to the access token of the explorer process
            if (!Win32API.OpenProcessToken(hProcess, Win32API.TOKEN_DUPLICATE, ref hPToken))
            {
                Win32API.CloseHandle(hProcess);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: OpenProcessToken: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return IntPtr.Zero;
            }

            Win32API.SECURITY_ATTRIBUTES sa = new Win32API.SECURITY_ATTRIBUTES();
            sa.Length = Marshal.SizeOf(sa);

            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: DuplicateTokenEx");
            // copy the access token of the winlogon process; the newly created token will be a primary token
            if (!Win32API.DuplicateTokenEx(hPToken, Win32API.MAXIMUM_ALLOWED, ref sa, (int)Win32API.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)Win32API.TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
            {
                Win32API.CloseHandle(hProcess);
                Win32API.CloseHandle(hPToken);
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: DuplicateTokenEx: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
                return IntPtr.Zero;
            }

            IntPtr lpEnvironment = IntPtr.Zero;
            Trace.WriteLineIf(_traceSwitch.TraceVerbose,  "GetUserEnvironement: CreateEnvironmentBlock");
            bool resultEnv = Win32API.CreateEnvironmentBlock(ref lpEnvironment, hUserTokenDup, false);
            if (resultEnv != true)
            {
                lpEnvironment = IntPtr.Zero;
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: CreateEnvironmentBlock: Code={0} Message={1}", Marshal.GetLastWin32Error(), GetLastWin32ErrorMessage()));
            }
            else
            {
                System.Collections.Generic.IEnumerable<string> str1 = ExtractMultiString(lpEnvironment);
                foreach (var item in str1)
                {
                    Trace.WriteLineIf(_traceSwitch.TraceVerbose,  string.Format("GetUserEnvironement: CreateEnvironmentBlock Environment: {0}", item.ToString()));
                }
            }
            // Invalidate handles.
            Win32API.CloseHandle(hProcess);
            Win32API.CloseHandle(hPToken);

            return lpEnvironment;
        }

        #endregion
    }

}