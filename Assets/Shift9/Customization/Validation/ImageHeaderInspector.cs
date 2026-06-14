namespace Shift9.Customization.Validation
{
    public enum ImageFormat : byte { Unknown = 0, Png, Jpeg }

    /// <summary>
    /// Sniffs image format and dimensions from raw header bytes WITHOUT decoding the pixels.
    /// This is the decompression-bomb defense: we learn a 100,000x100,000 image is hostile
    /// from ~24 header bytes, before LoadImage ever allocates gigabytes of texture memory.
    /// Format is determined by magic bytes, never by file extension.
    /// </summary>
    public static class ImageHeaderInspector
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// Returns the detected format and dimensions. Returns false (format Unknown) if the
        /// bytes are not a recognized/whitelisted image or are too short to read a header.
        /// </summary>
        public static bool TryInspect(byte[] data, out ImageFormat format, out int width, out int height)
        {
            format = ImageFormat.Unknown;
            width = 0; height = 0;
            if (data == null) return false;

            if (IsPng(data)) return TryReadPng(data, out width, out height, ref format);
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
                return TryReadJpeg(data, out width, out height, ref format);

            return false;
        }

        private static bool IsPng(byte[] d)
        {
            if (d.Length < PngSignature.Length) return false;
            for (int i = 0; i < PngSignature.Length; i++)
                if (d[i] != PngSignature[i]) return false;
            return true;
        }

        // PNG: IHDR is the first chunk; width/height are big-endian uint32 at offsets 16 and 20.
        private static bool TryReadPng(byte[] d, out int w, out int h, ref ImageFormat fmt)
        {
            w = 0; h = 0;
            if (d.Length < 24) return false;
            w = BeInt32(d, 16);
            h = BeInt32(d, 20);
            if (w <= 0 || h <= 0) return false;
            fmt = ImageFormat.Png;
            return true;
        }

        // JPEG: walk the segment markers to the first Start-Of-Frame (SOF0..SOF3, excluding
        // non-frame markers). Height/width are big-endian uint16 at marker payload offsets 3/5.
        private static bool TryReadJpeg(byte[] d, out int w, out int h, ref ImageFormat fmt)
        {
            w = 0; h = 0;
            int i = 2; // past SOI (FF D8)
            int n = d.Length;
            while (i + 9 < n)
            {
                if (d[i] != 0xFF) { i++; continue; }       // resync to next marker
                byte marker = d[i + 1];

                // Standalone markers (no length): padding 0xFF, RSTn, SOI/EOI.
                if (marker == 0xFF) { i++; continue; }
                if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
                { i += 2; continue; }

                int segLen = BeInt16(d, i + 2);
                if (segLen < 2) return false;              // malformed

                bool isSof = (marker >= 0xC0 && marker <= 0xC3) ||
                             (marker >= 0xC5 && marker <= 0xC7) ||
                             (marker >= 0xC9 && marker <= 0xCB) ||
                             (marker >= 0xCD && marker <= 0xCF);
                if (isSof)
                {
                    if (i + 9 >= n) return false;
                    h = BeInt16(d, i + 5);
                    w = BeInt16(d, i + 7);
                    if (w <= 0 || h <= 0) return false;
                    fmt = ImageFormat.Jpeg;
                    return true;
                }
                i += 2 + segLen;                           // skip this segment
            }
            return false;
        }

        private static int BeInt32(byte[] d, int o) =>
            (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

        private static int BeInt16(byte[] d, int o) => (d[o] << 8) | d[o + 1];
    }
}
