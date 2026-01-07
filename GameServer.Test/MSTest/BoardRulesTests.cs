using Microsoft.VisualStudio.TestTools.UnitTesting;
using GooseGame.Core;

namespace GameServer.Tests
{
    [TestClass]
    public class BoardRulesTests
    {
        [TestMethod]
        public void Test_Move_Normal()
        {
            int current = 1;
            int d1 = 2;
            int d2 = 1;

            var result = BoardRulesEngine.CalculateMove(current, d1, d2, "Tester");

            Assert.AreEqual(4, result.FinalPosition);
            Assert.IsFalse(result.IsExtraTurn);
        }

        [TestMethod]
        public void Test_Goose_Jump()
        {
            int current = 1;
            int d1 = 2;
            int d2 = 2;

            var result = BoardRulesEngine.CalculateMove(current, d1, d2, "Tester");

            Assert.AreEqual(9, result.FinalPosition);
            Assert.IsTrue(result.IsExtraTurn);
            Assert.IsTrue(result.Message.Contains("Oca"));
        }

        [TestMethod]
        public void Test_Skull_Death()
        {
            int current = 50;
            int d1 = 4;
            int d2 = 4;

            var result = BoardRulesEngine.CalculateMove(current, d1, d2, "Tester");

            Assert.AreEqual(1, result.FinalPosition);
            Assert.IsTrue(result.Message.Contains("CALAVERA"));
        }

        [TestMethod]
        public void Test_Win_Condition()
        {
            var result = BoardRulesEngine.CalculateMove(60, 2, 2, "Winner");

            Assert.AreEqual(64, result.FinalPosition);
            Assert.AreEqual("WIN", result.Message);
        }

        [TestMethod]
        public void Test_Bridge_Forward()
        {
            var result = BoardRulesEngine.CalculateMove(1, 3, 2, "Tester");

            Assert.AreEqual(12, result.FinalPosition);
            Assert.IsTrue(result.IsExtraTurn);
        }
    }
}