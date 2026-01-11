#nullable disable
using GameServer.GameEngines;
using GameServer.Services.Common;
using Moq;
using Xunit;

namespace GameServer.Tests.Unit
{
    public sealed class GooseBoardEngineTests
    {
        private readonly Mock<IDiceRoller> _rollerMock;
        private readonly GooseBoardEngine _engine;

        private int _coins = 100;
        private int _tCommon = 0;
        private int _tEpic = 0;
        private int _tLegendary = 0;
        private const string USER = "Player1";

        public GooseBoardEngineTests()
        {
            _rollerMock = new Mock<IDiceRoller>();
            _engine = new GooseBoardEngine(_rollerMock.Object);
        }

        [Fact]
        public void GenerateDiceRoll_Under60_ReturnsTwoDice()
        {
            _rollerMock.SetupSequence(r => r.Next(1, 7))
                .Returns(3)
                .Returns(4);

            var result = _engine.GenerateDiceRoll(50);

            Assert.Equal(3, result.D1);
            Assert.Equal(4, result.D2);
        }

        [Fact]
        public void GenerateDiceRoll_Over60_ReturnsOneDie()
        {
            _rollerMock.Setup(r => r.Next(1, 7)).Returns(5);

            var result = _engine.GenerateDiceRoll(61);

            Assert.Equal(5, result.D1);
            Assert.Equal(0, result.D2);
        }

        [Fact]
        public void CalculateBoardRules_Win_ReturnsWinMessage()
        {
            var result = _engine.CalculateBoardRules(64, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(64, result.FinalPosition);
            Assert.Equal("WIN", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_BounceBack_CorrectPosition()
        {
            var result = _engine.CalculateBoardRules(66, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(62, result.FinalPosition);
        }

        [Theory]
        [InlineData(5, 9)]
        [InlineData(9, 18)]
        [InlineData(18, 23)]
        [InlineData(23, 27)]
        [InlineData(27, 32)]
        [InlineData(32, 36)]
        [InlineData(36, 41)]
        [InlineData(41, 45)]
        [InlineData(45, 50)]
        [InlineData(50, 54)]
        [InlineData(54, 59)]
        public void CalculateBoardRules_Goose_JumpsToNextGoose(int startPos, int expectedPos)
        {
            var result = _engine.CalculateBoardRules(startPos, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(expectedPos, result.FinalPosition);
            Assert.True(result.IsExtraTurn);
            Assert.Contains("Oca a Oca", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_LastGoose_StayAndShootAgain()
        {
            var result = _engine.CalculateBoardRules(59, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(59, result.FinalPosition);
            Assert.True(result.IsExtraTurn);
            Assert.Contains("Oca (59)", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_BridgeForward_JumpsTo12()
        {
            var result = _engine.CalculateBoardRules(6, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(12, result.FinalPosition);
            Assert.True(result.IsExtraTurn);
            Assert.Contains("Puente a Puente", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_BridgeBackward_ReturnsTo6()
        {
            var result = _engine.CalculateBoardRules(12, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(6, result.FinalPosition);
            Assert.True(result.IsExtraTurn);
            Assert.Contains("Puente a Puente", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Labyrinth_ReturnsTo30()
        {
            var result = _engine.CalculateBoardRules(42, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(30, result.FinalPosition);
            Assert.Contains("Laberinto", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Skull_ReturnsToStart()
        {
            var result = _engine.CalculateBoardRules(58, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(1, result.FinalPosition);
            Assert.Contains("CALAVERA", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Dice26_DoublesMove()
        {
            var result = _engine.CalculateBoardRules(26, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(52, result.FinalPosition);
            Assert.Contains("Dados", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Dice53_DoublesAndBounces()
        {
            var result = _engine.CalculateBoardRules(53, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(22, result.FinalPosition);
            Assert.Contains("Dados", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Inn_SkipsOneTurn()
        {
            var result = _engine.CalculateBoardRules(19, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(1, result.TurnsToSkip);
            Assert.Contains("Posada", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Well_SkipsTwoTurns()
        {
            var result = _engine.CalculateBoardRules(31, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(2, result.TurnsToSkip);
            Assert.Contains("Pozo", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_Prison_SkipsThreeTurns()
        {
            var result = _engine.CalculateBoardRules(56, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(3, result.TurnsToSkip);
            Assert.Contains("Cárcel", result.Message);
        }

        [Fact]
        public void CalculateBoardRules_LuckyBox_CoinsReward()
        {
            _rollerMock.Setup(r => r.Next(1, 101)).Returns(10);

            var result = _engine.CalculateBoardRules(7, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(150, _coins);
            Assert.Equal("COINS", result.Reward.Type);
            Assert.Equal(50, result.Reward.Amount);
            Assert.Contains("LUCKYBOX", result.LuckyBoxTag);
        }

        [Fact]
        public void CalculateBoardRules_LuckyBox_CommonTicket()
        {
            _rollerMock.Setup(r => r.Next(1, 101)).Returns(60);

            var result = _engine.CalculateBoardRules(14, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(1, _tCommon);
            Assert.Equal("COMMON", result.Reward.Type);
        }

        [Fact]
        public void CalculateBoardRules_LuckyBox_EpicTicket()
        {
            _rollerMock.Setup(r => r.Next(1, 101)).Returns(90);

            var result = _engine.CalculateBoardRules(25, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(1, _tEpic);
            Assert.Equal("EPIC", result.Reward.Type);
        }

        [Fact]
        public void CalculateBoardRules_LuckyBox_LegendaryTicket()
        {
            _rollerMock.Setup(r => r.Next(1, 101)).Returns(99);

            var result = _engine.CalculateBoardRules(34, USER, ref _coins, ref _tCommon, ref _tEpic, ref _tLegendary);

            Assert.Equal(1, _tLegendary);
            Assert.Equal("LEGENDARY", result.Reward.Type);
        }

        [Fact]
        public void BuildActionDescription_FormatsCorrectly()
        {
            var moveResult = new GooseBoardEngine.BoardMoveResult
            {
                FinalPosition = 10,
                Message = "TestMsg",
                IsExtraTurn = true,
                LuckyBoxTag = "[TAG]"
            };

            var desc = _engine.BuildActionDescription(USER, 3, 4, moveResult);

            Assert.Contains(USER, desc);
            Assert.Contains("3 y 4", desc);
            Assert.Contains("[EXTRA]", desc);
            Assert.Contains("[TAG]", desc);
            Assert.Contains("TestMsg", desc);
        }

        [Fact]
        public void BuildActionDescription_OneDie_FormatsCorrectly()
        {
            var moveResult = new GooseBoardEngine.BoardMoveResult { FinalPosition = 62 };
            var desc = _engine.BuildActionDescription(USER, 5, 0, moveResult);

            Assert.Contains("tiró 5.", desc);
            Assert.DoesNotContain("y 0", desc);
            Assert.Contains("Avanza a 62", desc);
        }
    }
}