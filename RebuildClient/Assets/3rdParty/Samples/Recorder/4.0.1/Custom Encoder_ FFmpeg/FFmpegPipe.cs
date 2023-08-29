//#define FFMPEGPIPE_TRACE_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditor.Recorder.Examples
{
    sealed class FFmpegPipe : IDisposable
    {
        static string _executablePath;
        #region Public methods

        internal static Process LaunchFFMPEG(string arguments)
        {
            #if FFMPEGPIPE_TRACE_ENABLED
            Debug.Log($"ffmpeg: {arguments}");
            #endif

            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    FileName = Path.GetFullPath(ExecutablePath),
                    Arguments = arguments
                },
                EnableRaisingEvents = true
            };
        }

        public FFmpegPipe(string arguments, string executablePath, string name = "")
        {
            _executablePath = executablePath;
            _name = name;
            // Start FFmpeg subprocess.

            _subprocess = LaunchFFMPEG(arguments);

            _subprocess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    Debug.LogWarning("FFmpegPipe(" + Thread.CurrentThread.ManagedThreadId + ")" + e.Data);
                }
            };

            _subprocess.Exited += delegate
            {
                Log("FFmpegPipe(" + Thread.CurrentThread.ManagedThreadId + ") exited");
            };
            _subprocess.Start();

            _subprocess.BeginErrorReadLine();

            _arguments = arguments;
            Log(string.Format("Encoding with cmdline: ffmpeg {0}", _arguments));
            // Start copy/pipe subthreads.
            _copyThread = new Thread(CopyThread);
            _pipeThread = new Thread(PipeThread);
            _copyThread.Start();
            _pipeThread.Start();
        }

        internal void PushFrameData(NativeArray<byte> data)
        {
            Log("VideoFrame: " + videoFrameCount++);

            // Update the copy queue and notify the copy thread with a ping.
            lock (_copyQueue) _copyQueue.Enqueue(data);
            _copyPing.Set();
        }

        internal unsafe void PushFrameData(NativeArray<float> data)
        {
            Log("AudioFrame: " + audioFrameCount++);

            // Convert NativeArray of float to a byteArray
            var length = data.Length * sizeof(float);
            var byteArray = new NativeArray<byte>(length, Allocator.Temp);
            var dataPtr = data.GetUnsafePtr();
            var bytePtr = byteArray.GetUnsafePtr();
            Buffer.MemoryCopy(dataPtr, bytePtr, length, length);

            // Update the copy queue and notify the copy thread with a ping.
            lock (_copyQueue) _copyQueue.Enqueue(byteArray);
            _copyPing.Set();
        }

        internal void SyncFrameData()
        {
            int nbRetries = 0;
            // Wait for the copy queue to get emptied using pong
            // notification signals sent from the copy thread.
            while (_copyQueue.Count > 0)
            {
                if (!_copyPong.WaitOne(_timeoutValue))
                {
                    if (nbRetries++ > _maxRetries)
                    {
                        Log("SyncFrameData timeout for ffmpeg pipe of " +
                            _name + "_copyQueue.Count = " + _copyQueue.Count);
                        _terminate = true;
                        return;
                    }
                }
            }

            nbRetries = 0;
            // When using a slower codec (e.g. HEVC, ProRes), frames may be
            // queued too much, and it may end up with an out-of-memory error.
            // To avoid this problem, we wait for pipe queue entries to be
            // comsumed by the pipe thread.
            while (_pipeQueue.Count > 4)
            {
                Log("Sync WaitOne pipe " + _name);
                if (!_pipePong.WaitOne(_timeoutValue))
                {
                    if (nbRetries++ > _maxRetries)
                    {
                        Log("SyncFrameData timeout for ffmpeg pipe of  " +
                            _name + "_pipeQueue.Count = " + _pipeQueue.Count);
                        _terminate = true;
                        return;
                    }
                }
            }
        }

        internal string CloseAndGetOutput()
        {
            // Terminate the subthreads.
            _terminate = true;

            _copyPing.Set();
            _pipePing.Set();

            _copyThread.Join();
            _pipeThread.Join();

            // Close FFmpeg subprocess.
            _subprocess.StandardInput.Close();
            _subprocess.WaitForExit(_timeoutValue);

            _subprocess.Close();
            _subprocess.Dispose();


            // Nullify members (just for ease of debugging).
            _subprocess = null;
            _copyThread = null;
            _pipeThread = null;

            _copyQueue = null;
            _pipeQueue = _freeBuffer = null;
            return "";
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            if (!_terminate) CloseAndGetOutput();
        }

        ~FFmpegPipe()
        {
            if (!_terminate)
                Debug.LogWarning(
                    "An unfinalized FFmpegPipe object was detected. " +
                    "It should be explicitly closed or disposed " +
                    "before being garbage-collected."
                );
        }

        #endregion

        #region Private members

        Process _subprocess;
        Thread _copyThread;
        Thread _pipeThread;

        AutoResetEvent _copyPing = new AutoResetEvent(false);
        AutoResetEvent _copyPong = new AutoResetEvent(false);
        AutoResetEvent _pipePing = new AutoResetEvent(false);
        AutoResetEvent _pipePong = new AutoResetEvent(false);
        bool _terminate;
        string _name;
        int videoFrameCount;
        int audioFrameCount;

        Queue<NativeArray<byte>> _copyQueue = new Queue<NativeArray<byte>>();
        Queue<byte[]> _pipeQueue = new Queue<byte[]>();
        Queue<byte[]> _freeBuffer = new Queue<byte[]>();
        int _timeoutValue = 500; // .5 sec
        int _maxRetries = 2;
        string _arguments;

        internal static string ExecutablePath => _executablePath;

        internal void Log(string log)
        {
#if FFMPEGPIPE_TRACE_ENABLED
            Debug.Log("FFmpegPipe : " + log);
#endif
        }

        #endregion

        #region Subthread entry points

        // CopyThread - Copies frames given from the readback queue to the pipe
        // queue. This is required because readback buffers are not under our
        // control -- they'll be disposed before being processed by us. They
        // have to be buffered by end-of-frame.
        void CopyThread()
        {
            int nbTries = 0;
            while (!_terminate)
            {
                // Wait for ping from the main thread.
                if (!_copyPing.WaitOne(_timeoutValue))
                {
                    nbTries++;
                    if (nbTries > _maxRetries)
                    {
                        Log("CopyThread timeout for ffmpeg pipe of file " +
                            _name + "_copyQueue.Count = " + _copyQueue.Count);
                        _terminate = true;
                        return;
                    }
                }

                // Process all entries in the copy queue.
                while (_copyQueue.Count > 0)
                {
                    // Retrieve an copy queue entry without dequeuing it.
                    // (We don't want to notify the main thread at this point.)
                    NativeArray<byte> source;
                    lock (_copyQueue) source = _copyQueue.Peek();

                    // Try allocating a buffer from the free buffer list.
                    byte[] buffer = null;
                    if (_freeBuffer.Count > 0)
                        lock (_freeBuffer)
                            buffer = _freeBuffer.Dequeue();

                    // Copy the contents of the copy queue entry.
                    if (buffer == null || buffer.Length != source.Length)
                        buffer = source.ToArray();
                    else
                        source.CopyTo(buffer);

                    // Push the buffer entry to the pipe queue.
                    lock (_pipeQueue) _pipeQueue.Enqueue(buffer);
                    _pipePing.Set(); // Ping the pipe thread.

                    // Dequeue the copy buffer entry and ping the main thread.
                    lock (_copyQueue) _copyQueue.Dequeue();
                    _copyPong.Set();
                }
            }
        }

        // PipeThread - Receives frame entries from the copy thread and push
        // them into the FFmpeg pipe.
        void PipeThread()
        {
            int nbTries = 0;

            var pipe = _subprocess.StandardInput.BaseStream;

            while (!_terminate)
            {
                // Wait for the ping from the copy thread.
                if (!_pipePing.WaitOne(_timeoutValue))
                {
                    nbTries++;
                    if (nbTries > _maxRetries)
                    {
                        Log("PipeThread for ffmpeg pipe of file " +
                            _name + "_pipeQueue.Count = " + _pipeQueue.Count);
                        _terminate = true;
                        return;
                    }
                }

                // Process all entries in the pipe queue.
                while (_pipeQueue.Count > 0)
                {
                    // Retrieve a frame entry.
                    byte[] buffer;
                    lock (_pipeQueue) buffer = _pipeQueue.Dequeue();

                    // Write it into the FFmpeg pipe.
                    try
                    {
                        pipe.Write(buffer, 0, buffer.Length);
                        pipe.Flush();
                    }
                    catch
                    {
                        // Pipe.Write could raise an IO exception when ffmpeg
                        // is terminated for some reason.
                        _terminate = true;
                        Log("PipeThread writing to ffmpeg pipe cause an exception");
                        return;
                    }

                    // Add the buffer to the free buffer list to reuse later.
                    lock (_freeBuffer) _freeBuffer.Enqueue(buffer);
                    _pipePong.Set();
                }
            }
        }

        #endregion
    }
}
