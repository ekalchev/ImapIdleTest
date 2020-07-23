using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ImapIdleTest
{
    class Test
    {
        private CancellationTokenSource doneTokenSource = new CancellationTokenSource();
        private CancellationTokenSource disconnectTokenSource = new CancellationTokenSource();
        private Thread workerThread;
        private readonly string id;
        private readonly bool useAsync;
        public event EventHandler IdleCancellationFailed;

        public Test(bool useAsync)
        {
            StartIdle();
            id = useAsync ? "Async Idle" : "Sync Idle";
            this.useAsync = useAsync;
        }

        private void StartIdle()
        {
            if (workerThread == null)
            {
                workerThread = new Thread(ThreadProc);
                workerThread.Start();
                workerThread.IsBackground = true;
            }
        }

        public void Destroy()
        {
            if(workerThread != null)
            {
                Logger.Log($"Thread {id} was aborted");
                workerThread.Abort();
            }
        }

        public void Cancel()
        {
            doneTokenSource.Cancel();
            disconnectTokenSource.Cancel();

            Task.Run(async () =>
            {
                while (workerThread.IsAlive == true)
                {
                    await Task.Delay(5000);
                    
                    // if 5 sec after cancellation the thread thread is still alive that means
                    // imap.Idle was not cancelled
                    if(workerThread.IsAlive == true)
                    {
                        Logger.Log($"Idle {workerThread.Name}: was not cancelled!!!!");

                        try
                        {
                            Process.Start(Path.Combine(Directory.GetCurrentDirectory(), "procdump.exe"), "-ma " + Process.GetCurrentProcess().Id.ToString());
                        }
                        catch(Exception)
                        {

                        }

                        await Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            workerThread = null;
                            IdleCancellationFailed?.Invoke(this, EventArgs.Empty);
                        });
                    }
                }

                Logger.Log($"Idle {workerThread.Name}: was cancelled");
            });
        }

        private void OpenInbox(ImapClient imapClient)
        {
            try
            {
                if (imapClient.IsConnected == false)
                {
                    imapClient.Connect("mail.mobisystems.com", 993, SecureSocketOptions.SslOnConnect, default);
                    imapClient.Authenticate("test1@mobisystems.com", "T%est654!!321");
                }

                imapClient.Inbox.Open(FolderAccess.ReadWrite);
            }
            catch(Exception ex)
            {
                Logger.Log($"{id}: {ex.Message}");
            }
        }

        private void ThreadProc()
        {
            Thread.CurrentThread.Name = $"Imap Thread {id}";
            Logger.Log($"Thread started: {id}");
            ImapClient imapClient = new ImapClient();
            OpenInbox(imapClient);

            while (doneTokenSource.Token.IsCancellationRequested == false && disconnectTokenSource.Token.IsCancellationRequested == false)
            {
                // restart idle every 5min to avoid server disconnects because of a timeout
                var timeoutCancellationTokenSource =
#if DEBUG
                    new CancellationTokenSource(3 * 1000);
#else
                    new CancellationTokenSource(3 * 60 * 1000);
#endif
                CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(doneTokenSource.Token, timeoutCancellationTokenSource.Token);

                OpenInbox(imapClient);

                try
                {
                    Logger.Log($"{id}: Imap idle starting");

                    if (useAsync)
                    {
                        imapClient.IdleAsync(linkedTokenSource.Token, disconnectTokenSource.Token).GetAwaiter().GetResult();
                    }
                    else
                    {
                        imapClient.Idle(linkedTokenSource.Token, disconnectTokenSource.Token);
                    }

                    Logger.Log($"{id}: Imap idle complete");
                }
                catch (ThreadAbortException)
                {
                    Logger.Log($"ThreadAbortException");
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    Logger.Log($"{id}: {ex.Message}");
                }

                Thread.Sleep(3000);
            }

            if (imapClient.IsConnected)
            {
                imapClient.Disconnect(true);
            }

            Logger.Log($"Thread exit");
        }
    }
}
