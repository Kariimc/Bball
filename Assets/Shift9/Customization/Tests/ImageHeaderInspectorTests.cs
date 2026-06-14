using NUnit.Framework;
using Shift9.Customization.Validation;

namespace Shift9.Customization.Tests
{
    public sealed class ImageHeaderInspectorTests
    {
        [Test]
        public void ReadsPngDimensions()
        {
            Assert.IsTrue(ImageHeaderInspector.TryInspect(TestData.Png(640, 480), out var f, out int w, out int h));
            Assert.AreEqual(ImageFormat.Png, f);
            Assert.AreEqual(640, w);
            Assert.AreEqual(480, h);
        }

        [Test]
        public void ReadsJpegDimensions()
        {
            Assert.IsTrue(ImageHeaderInspector.TryInspect(TestData.Jpeg(320, 200), out var f, out int w, out int h));
            Assert.AreEqual(ImageFormat.Jpeg, f);
            Assert.AreEqual(320, w);
            Assert.AreEqual(200, h);
        }

        [Test]
        public void RejectsUnknownAndShort()
        {
            Assert.IsFalse(ImageHeaderInspector.TryInspect(TestData.Utf8("GIF89a..."), out _, out _, out _));
            Assert.IsFalse(ImageHeaderInspector.TryInspect(new byte[] { 0x89, 0x50 }, out _, out _, out _));
            Assert.IsFalse(ImageHeaderInspector.TryInspect(null, out _, out _, out _));
        }
    }
}
