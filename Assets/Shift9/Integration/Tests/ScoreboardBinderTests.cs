using NUnit.Framework;
using Shift9.Integration;

namespace Shift9.Integration.Tests
{
    public sealed class ScoreboardBinderTests
    {
        [Test]
        public void Abbreviate_MultiWordTakesInitials()
        {
            Assert.AreEqual("NY", ScoreboardBinder.Abbreviate("New York"));
            Assert.AreEqual("SA", ScoreboardBinder.Abbreviate("San Antonio"));
            Assert.AreEqual("GSW", ScoreboardBinder.Abbreviate("Golden State Warriors"));
        }

        [Test]
        public void Abbreviate_SingleWordTakesFirstThree()
        {
            Assert.AreEqual("LAK", ScoreboardBinder.Abbreviate("Lakers"));
            Assert.AreEqual("OKC", ScoreboardBinder.Abbreviate("OKC"));
        }

        [Test]
        public void Abbreviate_EmptyOrNull()
        {
            Assert.AreEqual("", ScoreboardBinder.Abbreviate(null));
            Assert.AreEqual("", ScoreboardBinder.Abbreviate("   "));
        }
    }
}
