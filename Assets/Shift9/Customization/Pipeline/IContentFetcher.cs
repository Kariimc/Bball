using System.Threading;
using System.Threading.Tasks;

namespace Shift9.Customization.Pipeline
{
    /// <summary>Result of a single remote fetch. <see cref="Data"/> is null on failure.</summary>
    public readonly struct FetchResponse
    {
        public readonly bool Success;
        public readonly byte[] Data;
        public readonly long StatusCode;
        public readonly string Error;

        private FetchResponse(bool ok, byte[] data, long status, string error)
        {
            Success = ok; Data = data; StatusCode = status; Error = error;
        }

        public static FetchResponse Ok(byte[] data, long status) => new FetchResponse(true, data, status, null);
        public static FetchResponse Fail(string error, long status = 0) => new FetchResponse(false, null, status, error);
    }

    /// <summary>
    /// Transport abstraction for the import pipeline. Production uses <c>UnityWebFetcher</c>;
    /// tests inject a fake. <paramref name="maxBytes"/> MUST be enforced during streaming so a
    /// hostile server cannot exhaust memory before the size check runs.
    /// </summary>
    public interface IContentFetcher
    {
        Task<FetchResponse> FetchAsync(string url, long maxBytes, CancellationToken ct);
    }
}
