using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE80;


namespace FG.CheckoutAndBuild2
{
    /// <summary>
    /// Debug hilfsfunktionen
    /// </summary>
    public static class DebugHelper
    {
        /// <summary>
        /// Ist Debug
        /// </summary>
        public static bool IsDebug
        {

#if DEBUG
            get { return true; }
#else
            get { return false; }
#endif
        }

        /// <summary>
        /// Einen Process Attachen
        /// </summary>
        [Conditional("DEBUG")]
        public static void AttachProcess(int processId, DebugEngineType debugEngine = DebugEngineType.Automatic)
        {
            bool attached = false;
            //did not find a better solution for this(since it's not super reliable)
            for (int i = 0; i < 5; i++)
            {
                if (attached)
                    break;
                try
                {
                    var dte2 = GetDTE();

                    MessageFilter.Register();
                    dte2.MainWindow.Activate();
                    var debugger = (Debugger2)dte2.Debugger;
                    var program = GetProcess(dte2, processId);
                    if (program != null)
                    {
                        AttachProcess(program, debugger, debugEngine);
                        attached = true;
                    }
                }
                finally
                {
                    MessageFilter.Revoke();
                }
            }
        }

        /// <summary>
        /// Einen Process Attachen
        /// </summary>
        [Conditional("DEBUG")]
        public static void AttachProcess(string processName, DebugEngineType debugEngine = DebugEngineType.Automatic)
        {
            bool attached = false;
            //did not find a better solution for this(since it's not super reliable)
            for (int i = 0; i < 5; i++)
            {
                if (attached)
                    break;

                try
                {
                    var dte2 = GetDTE();

                    MessageFilter.Register();
                    dte2.MainWindow.Activate();
                    var debugger = (Debugger2)dte2.Debugger;

                    foreach (Process2 process in debugger.LocalProcesses)
                    {
                        if (process.Name.Contains(processName))
                        {
                            AttachProcess(process, debugger, debugEngine);
                            attached = true;
                        }
                    }
                }
                finally
                {
                    MessageFilter.Revoke();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="procId"></param>
        [Conditional("DEBUG")]
        public static void OpenInternetExplorerDeveloperTools(int procId = -1)
        {
            if (procId == -1)
            {
                foreach (var p in System.Diagnostics.Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("iexplore")))
                {
                    procId = p.Id;
                    break;
                }
            }
            var process = System.Diagnostics.Process.GetProcessById(procId);
            var handle = process.MainWindowHandle;
            var b = SetForegroundWindow(handle);
            if (b)
            {
                keybd_event(VK_F12, 0, 0, 0);// presses F12
                keybd_event(VK_F12, 0, 2, 0); //releases F12	
            }
        }

        /// <summary>
        /// IE Script Debugging für angegebene Url
        /// </summary>
        [Conditional("DEBUG")]
        public static void AttachScriptDebugger(string url, bool startNewIE = true)
        {
            int id;

            if (startNewIE)
            {
                foreach (var s in System.Diagnostics.Process.GetProcesses().Where(process => process.ProcessName.ToLower().Contains("iexplore")))
                    s.Kill();
                id = System.Diagnostics.Process.Start("iexplore").Id;
            }
            else
            {
                id = System.Diagnostics.Process.GetProcesses().FirstOrDefault(process => process.ProcessName.ToLower().Contains("iexplore")).Id;
            }


            System.Threading.Thread.Sleep(1000);
            WaitFor("iexplore", TimeSpan.FromSeconds(2));
            System.Threading.Thread.Sleep(1500);
            AttachProcess("iexplore", DebugEngineType.Script);
            System.Threading.Thread.Sleep(200);
            OpenInternetExplorerDeveloperTools(id);
            System.Threading.Thread.Sleep(500);
            OpenUrlInRunningInternetExplorer(url);
        }

        /// <summary>
        /// Detach all attached processes
        /// </summary>
        [Conditional("DEBUG")]
        public static void DetachAll()
        {
            GetDTE().Debugger.DetachAll();
        }

        /// <summary>
        /// Auf process warten
        /// </summary>
        [Conditional("DEBUG")]
        public static void WaitFor(string processName, TimeSpan timeout)
        {
            Stopwatch watch = Stopwatch.StartNew();
            System.Diagnostics.Process p = null;
            do
            {
                p = System.Diagnostics.Process.GetProcesses().FirstOrDefault(process => process.ProcessName.ToLower().Contains(processName.ToLower()));
                System.Threading.Thread.Sleep(100);

            } while (p == null || watch.Elapsed > timeout);
        }

        private static Process2 GetProcess(DTE2 dte2, int processId)
        {
            MessageFilter.Register();
            dte2.MainWindow.Activate();
            var debugger = dte2.Debugger;
            return debugger.LocalProcesses.Cast<Process2>().FirstOrDefault(localProcess => localProcess.ProcessID == processId);
        }

        private static Process2 GetProcess(DTE2 dte2, string processName)
        {
            MessageFilter.Register();
            dte2.MainWindow.Activate();
            var debugger = dte2.Debugger;
            return debugger.LocalProcesses.Cast<Process2>().FirstOrDefault(localProcess => localProcess.Name.ToLower().Contains(processName.ToLower()));
        }

        private static void AttachProcess(Process2 process, Debugger2 debugger, DebugEngineType debugEngine = DebugEngineType.Automatic)
        {
            if (debugEngine != DebugEngineType.Automatic)
            {
                EnvDTE80.Transport trans = debugger.Transports.Item("Default");
                EnvDTE80.Engine[] dbgeng = new EnvDTE80.Engine[2];
                dbgeng[0] = trans.Engines.Item("Script");
                //EnvDTE80.Process2 proc2 = (Process2)dbg2.GetProcesses(trans, "HILLR1").Item("iexplore.exe");
                process.Attach2(dbgeng);

            }
            else
            {
                process.Attach();
            }
        }

        private static DTE2 GetDTE()
        {
            return CheckoutAndBuild2Package.GetGlobalService<DTE2>();
            //DTE2 dte2 = null;
            //try
            //{
            //    dte2 = (DTE2)Marshal.GetActiveObject("VisualStudio.DTE.11.0");
            //}
            //catch (COMException)                                                // TODO: 2013-02-05 KLI/WIT - SCHEISSE!!!
            //{
            //    dte2 = (DTE2)Marshal.GetActiveObject("VisualStudio.DTE.10.0");
            //}
            //return dte2;
        }


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int VK_F12 = 0x7B;

        private static void OpenUrlInRunningInternetExplorer(string url)
        {
            var t = Type.GetTypeFromProgID("Shell.Application");
            dynamic o = Activator.CreateInstance(t);
            try
            {
                var ws = o.Windows();
                for (int i = 0; i < ws.Count; i++)
                {
                    var ie = ws.Item(i);
                    if (ie == null) continue;
                    var path = System.IO.Path.GetFileName((string)ie.FullName).ToLower();
                    if (path == "iexplore.exe")
                    {
                        ie.Navigate(url);
                    }
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(o);
            }
        }



        #region DTE_Stuff for Attach

        private class MessageFilter : IOleMessageFilter
        {
            //
            // Class containing the IOleMessageFilter
            // thread error-handling functions.

            // Start the filter.
            public static void Register()
            {
                IOleMessageFilter newFilter = new MessageFilter();
                IOleMessageFilter oldFilter = null;
                CoRegisterMessageFilter(newFilter, out oldFilter);
            }

            // Done with the filter, close it.
            public static void Revoke()
            {
                IOleMessageFilter oldFilter = null;
                CoRegisterMessageFilter(null, out oldFilter);
            }

            //
            // IOleMessageFilter functions.
            // Handle incoming thread requests.
            int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
            {
                //Return the flag SERVERCALL_ISHANDLED.
                return 0;
            }

            // Thread call was rejected, so try again.
            int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
            {
                if (dwRejectType == 2)
                // flag = SERVERCALL_RETRYLATER.
                {
                    // Retry the thread call immediately if return >=0 & 
                    // <100.
                    return 99;
                }
                // Too busy; cancel call.
                return -1;
            }

            int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
            {
                //Return the flag PENDINGMSG_WAITDEFPROCESS.
                return 2;
            }

            // Implement the IOleMessageFilter interface.
            [DllImport("Ole32.dll")]
            private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
        }

        [ComImport]
        [Guid("00000016-0000-0000-C000-000000000046")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleMessageFilter
        {
            [PreserveSig]
            int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
            [PreserveSig]
            int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
            [PreserveSig]
            int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
        }

        #endregion

    }

    public enum DebugEngineType
    {
        Automatic,
        Script
    }
}