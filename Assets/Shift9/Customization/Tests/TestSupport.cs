using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shift9.Customization.Pipeline;

namespace Shift9.Customization.Tests
{
    /// <summary>Deterministic in-memory fetcher for tests. Counts calls; honors the byte cap.</summary>
    internal sealed class FakeFetcher : IContentFetcher
    {
        public readonly Dictionary<string, byte[]> Map = new();
        public Func<string, FetchResponse> Override;
        public int Calls;

        public Task<FetchResponse> FetchAsync(string url, long maxBytes, CancellationToken ct)
        {
            Calls++;
            if (Override != null) return Task.FromResult(Override(url));
            if (Map.TryGetValue(url, out var bytes))
            {
                if (bytes.LongLength > maxBytes)
                    return Task.FromResult(FetchResponse.Fail("Payload exceeded byte cap.", 413));
                return Task.FromResult(FetchResponse.Ok(bytes, 200));
            }
            return Task.FromResult(FetchResponse.Fail("Not found", 404));
        }
    }

    /// <summary>Byte/string fixtures. Images are HEADER-ONLY (ContentValidator inspects headers,
    /// never decodes), which is exactly what the raw cache + pipeline paths exercise.</summary>
    internal static class TestData
    {
        public static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);

        /// <summary>A minimal PNG: 8-byte signature + IHDR carrying the given dimensions.</summary>
        public static byte[] Png(int width, int height)
        {
            var p = new byte[33];
            byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            Array.Copy(sig, p, 8);
            p[8] = 0; p[9] = 0; p[10] = 0; p[11] = 13;        // IHDR length
            p[12] = (byte)'I'; p[13] = (byte)'H'; p[14] = (byte)'D'; p[15] = (byte)'R';
            WriteBe32(p, 16, width);
            WriteBe32(p, 20, height);
            return p;
        }

        /// <summary>A minimal JPEG: SOI + SOF0 carrying the given dimensions.</summary>
        public static byte[] Jpeg(int width, int height)
        {
            var p = new byte[20];
            p[0] = 0xFF; p[1] = 0xD8;                          // SOI
            p[2] = 0xFF; p[3] = 0xC0;                          // SOF0
            p[4] = 0x00; p[5] = 0x11;                          // segment length (17)
            p[6] = 0x08;                                       // precision
            p[7] = (byte)(height >> 8); p[8] = (byte)height;   // height BE
            p[9] = (byte)(width >> 8);  p[10] = (byte)width;    // width BE
            return p;
        }

        public static string LeagueJson() =>
            "{\"schema\":1,\"league\":{\"id\":\"wnba\",\"name\":\"W <b>League</b>\",\"type\":\"WNBA\"}," +
            "\"arenas\":[{\"id\":\"a1\",\"name\":\"Center\",\"floorUrl\":\"https://cdn.example.com/floor.png\"," +
            "\"crowdDensity\":1.7,\"lightingPreset\":\"primetime\"}]," +
            "\"teams\":[{\"id\":\"t1\",\"name\":\"Liberty\",\"arenaId\":\"a1\",\"primary\":\"#1d428a\"," +
            "\"secondary\":\"bad\",\"uniforms\":[" +
            "{\"slot\":\"Home\",\"baseUrl\":\"https://cdn.example.com/home.png\"}," +
            "{\"slot\":\"Away\",\"baseUrl\":\"http://insecure.example.com/away.png\"}]," +
            "\"players\":[{\"id\":\"p1\",\"name\":\"Star\",\"number\":250,\"attributes\":{\"speed\":150,\"threePoint\":80}}]}]}";

        public static string SneakerJson() =>
            "{\"schema\":1,\"sneakers\":[" +
            "{\"id\":\"s1\",\"name\":\"Custom CW\",\"imageUrl\":\"https://cdn.example.com/kicks.png\"}," +
            "{\"id\":\"s2\",\"name\":\"Bad\",\"imageUrl\":\"http://insecure/kicks.png\"}]}";

        private static void WriteBe32(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }
    }
}
