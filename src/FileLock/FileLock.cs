using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace FileLock
{
    public class FileLock: IDisposable
    {
        private string FileName;

        private DateTime LastWriteTimeUtc = DateTime.MinValue;

        private int Timeout = 500;

        private int CheckTime = 500;

        private int LifeTime = 10;

        private System.Threading.Thread CheckThread;

        private System.Threading.ManualResetEvent StopEvent = new System.Threading.ManualResetEvent(false);

        private readonly object ThreadLock = new object();

        public FileLock(string lockFile)
        {
            FileName = lockFile;
        }

        public FileLock(string lockFile, int timeout)
        {
            FileName = lockFile;
            Timeout = timeout;
        }

        public static FileLock Acquire(string lockFile, int lockTimeout)
        {
            FileLock FileLock = new FileLock(lockFile, lockTimeout);
            if (FileLock.Acquire())
            {
                return FileLock;
            }
            else
            {
                throw new LockNotAcquired();
            }
        }

        public bool Acquire()
        {
            return Acquire(Timeout);
        }

        public bool Acquire(int timeOut)
        {
            lock (ThreadLock)
            {
                if (string.IsNullOrEmpty(FileName))
                    return false;

                if (LastWriteTimeUtc != DateTime.MinValue)
                    return false;

                TimeSpan span = TimeSpan.FromMilliseconds(timeOut);
                DateTime start = DateTime.Now;
                TimeSpan life = TimeSpan.FromSeconds(LifeTime);

                while (true)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(FileName, FileMode.CreateNew, FileAccess.Write, FileShare.Delete))
                        {
                            LastWriteTimeUtc = File.GetLastWriteTimeUtc(FileName);
                        }
                        break;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // we don't have sufficient privileges to do that
                        return false;
                    }
                    catch (IOException)
                    {
                        try
                        {
                            if (File.Exists(FileName))
                            {
                                DateTime existingTimeUtc = File.GetLastWriteTimeUtc(FileName);
                                if (DateTime.Now.ToUniversalTime() - existingTimeUtc > life)
                                {
                                    File.Delete(FileName);
                                    continue;
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            return false;
                        }
                        if (DateTime.Now - start < span)
                        {
                            Thread.Sleep(50);
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                StopEvent.Reset();

                CheckThread = new Thread(() => Check());
                CheckThread.IsBackground = true;
                CheckThread.Start();

                return true;
            }
        }

        private void Check()
        {
            try
            {
                while (true)
                {
                    // block thread until check time elapses or is unblocked by Release()
                    if (StopEvent.WaitOne(CheckTime))
                    {
                        return;
                    }
                    if (!IsMine())
                    {
                        return;
                    }
                    if (!Touch())
                    {
                        return;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Debug.WriteLine("Abort");
            }
        }

        private bool IsMine()
        {
            if (!File.Exists(FileName))
                return false;
            if (LastWriteTimeUtc == DateTime.MinValue)
                return false;
            try
            {
                DateTime writeStamp = File.GetLastWriteTimeUtc(FileName);
                return writeStamp == LastWriteTimeUtc;
            }
            catch
            {
                return false;
            }
        }

        public bool Touch()
        {
            try
            {
                lock (ThreadLock)
                {
                    DateTime timeUtc = DateTime.Now.ToUniversalTime();
                    File.SetLastWriteTimeUtc(FileName, timeUtc);
                    LastWriteTimeUtc = timeUtc;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool Release()
        {
            lock (ThreadLock)
            {

                // unlock check thread
                StopEvent.Set();
                // force context switch
                Thread.Sleep(0);
                try
                {
                    if (!IsMine())
                    {
                        return false;
                    }
                    try
                    {
                        File.Delete(FileName);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                finally
                {
                    if (CheckThread != null)
                    {
                        if (CheckThread.IsAlive)
                            CheckThread.Abort();
                        CheckThread = null;
                    }
                    LastWriteTimeUtc = DateTime.MinValue;
                }
            }
        }

        public void Dispose()
        {
            Release();
        }
    }
}
