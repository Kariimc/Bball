using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Shift9.Customization.Pipeline
{
    /// <summary>
    /// Production transport over UnityWebRequest. Enforces the byte cap MID-STREAM via a custom
    /// download handler, so a hostile server streaming gigabytes is aborted the instant it crosses
    /// the limit — the bytes are never fully buffered. Runs on the main thread (UnityWebRequest
    /// requirement); awaits without blocking via a TaskCompletionSource on the async op.
    /// </summary>
    public sealed class UnityWebFetcher : IContentFetcher
    {
        private readonly int _timeoutSeconds;

        public UnityWebFetcher(int timeoutSeconds = 20) => _timeoutSeconds = timeoutSeconds;

        public Task<FetchResponse> FetchAsync(string url, long maxBytes, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<FetchResponse>();
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            var handler = new BoundedDownloadHandler(new byte[16 * 1024], maxBytes);
            req.downloadHandler = handler;
            req.timeout = _timeoutSeconds;
            req.redirectLimit = 5; // bound redirect chains

            CancellationTokenRegistration reg = ct.Register(() => { if (!req.isDone) req.Abort(); });

            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                reg.Dispose();
                try
                {
                    if (handler.Exceeded)
                        tcs.TrySetResult(FetchResponse.Fail("Payload exceeded byte cap.", req.responseCode));
                    else if (req.result != UnityWebRequest.Result.Success)
                        tcs.TrySetResult(FetchResponse.Fail(req.error, req.responseCode));
                    else
                        tcs.TrySetResult(FetchResponse.Ok(handler.Bytes, req.responseCode));
                }
                finally { req.Dispose(); }
            };
            return tcs.Task;
        }

        /// <summary>Aborts the transfer the moment accumulated bytes would exceed the cap.</summary>
        private sealed class BoundedDownloadHandler : DownloadHandlerScript
        {
            private readonly MemoryStream _buffer = new MemoryStream();
            private readonly long _max;
            public bool Exceeded { get; private set; }

            public BoundedDownloadHandler(byte[] scratch, long max) : base(scratch) => _max = max;

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (Exceeded || data == null || dataLength <= 0) return !Exceeded;
                if (_buffer.Length + dataLength > _max) { Exceeded = true; return false; } // abort
                _buffer.Write(data, 0, dataLength);
                return true;
            }

            public byte[] Bytes => _buffer.ToArray();
        }
    }
}
