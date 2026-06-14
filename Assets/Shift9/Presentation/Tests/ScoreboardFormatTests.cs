using NUnit.Framework;
using Shift9.Presentation;

namespace Shift9.Presentation.Tests
{
    public sealed class ScoreboardFormatTests
    {
        [Test]
        public void FormatClock_MinutesAndSeconds()
        {
            Assert.AreEqual("5:31", ScoreboardFormat.FormatClock(331f));
            Assert.AreEqual("4:18", ScoreboardFormat.FormatClock(258f));
            Assert.AreEqual("1:00", ScoreboardFormat.FormatClock(60f));
            Assert.AreEqual("2:05", ScoreboardFormat.FormatClock(125f));
        }

        [Test]
        public void FormatClock_UnderAMinuteShowsTenths()
        {
            Assert.AreEqual("45.3", ScoreboardFormat.FormatClock(45.3f));
            Assert.AreEqual("0.0", ScoreboardFormat.FormatClock(0f));
        }

        [Test]
        public void FormatClock_ClampsNegative()
        {
            Assert.AreEqual("0.0", ScoreboardFormat.FormatClock(-5f));
        }

        [Test]
        public void QuarterLabel_OrdinalsAndOvertime()
        {
            Assert.AreEqual("1ST", ScoreboardFormat.QuarterLabel(1));
            Assert.AreEqual("2ND", ScoreboardFormat.QuarterLabel(2));
            Assert.AreEqual("3RD", ScoreboardFormat.QuarterLabel(3));
            Assert.AreEqual("4TH", ScoreboardFormat.QuarterLabel(4));
            Assert.AreEqual("OT1", ScoreboardFormat.QuarterLabel(5));
            Assert.AreEqual("OT2", ScoreboardFormat.QuarterLabel(6));
        }
    }
}
