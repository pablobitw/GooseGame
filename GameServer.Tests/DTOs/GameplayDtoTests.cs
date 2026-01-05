using GameServer.DTOs.Gameplay;
using System.Collections.Generic;
using Xunit;

namespace GameServer.Tests.DTOs
{
    public class GameplayDtoTests
    {
        [Theory]
        [InlineData(1, 2, 3)]
        [InlineData(6, 6, 12)]
        [InlineData(3, 4, 7)]
        [InlineData(1, 1, 2)]
        [InlineData(5, 2, 7)]
        [InlineData(4, 4, 8)]
        [InlineData(6, 1, 7)]
        [InlineData(2, 2, 4)]
        [InlineData(5, 5, 10)]
        [InlineData(3, 3, 6)]
        public void DiceRollDto_Integrity(int d1, int d2, int total)
        {
            var dto = new DiceRollDto { DiceOne = d1, DiceTwo = d2, Total = total };
            Assert.Equal(d1, dto.DiceOne);
            Assert.Equal(d2, dto.DiceTwo);
            Assert.Equal(total, dto.Total);
        }

        [Theory]
        [InlineData("LOBBY001", "PlayerAlpha")]
        [InlineData("XJ29S1", "GooseMaster")]
        [InlineData("ROOM_99", "Tester")]
        [InlineData("GOLDEN", "Winner")]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("ABC", "123")]
        [InlineData("G4M3", "Us3r")]
        [InlineData("B4TTL3", "P1")]
        [InlineData("PRUEBA", "PABLO")]
        public void GameplayRequest_Integrity(string lobby, string user)
        {
            var dto = new GameplayRequest { LobbyCode = lobby, Username = user };
            Assert.Equal(lobby, dto.LobbyCode);
            Assert.Equal(user, dto.Username);
        }

        [Theory]
        [InlineData("UserA", 15, true, "/Avatars/1.png")]
        [InlineData("UserB", 0, false, null)]
        [InlineData("UserC", 63, true, "path/to/img")]
        [InlineData("Guest", 1, true, "")]
        [InlineData("Bot1", 10, false, "bot.png")]
        [InlineData("P1", 5, true, "p1.jpg")]
        [InlineData("P2", 20, false, "p2.jpg")]
        [InlineData("P3", 30, true, "p3.jpg")]
        [InlineData("P4", 40, false, "p4.jpg")]
        [InlineData("P5", 50, true, "p5.jpg")]
        public void PlayerPositionDto_Integrity(string user, int tile, bool online, string path)
        {
            var dto = new PlayerPositionDto { Username = user, CurrentTile = tile, IsOnline = online, AvatarPath = path };
            Assert.Equal(user, dto.Username);
            Assert.Equal(tile, dto.CurrentTile);
            Assert.Equal(online, dto.IsOnline);
            Assert.Equal(path, dto.AvatarPath);
        }

        [Fact]
        public void GameStateDto_ListInitialization_VerifyIntegrity()
        {
            var state = new GameStateDto
            {
                CurrentTurnUsername = "Player1",
                GameLog = new List<string> { "Start", "Move 1" },
                PlayerPositions = new List<PlayerPositionDto> { new PlayerPositionDto { Username = "Player1" } },
                IsKicked = false,
                IsBanned = false,
                WinnerUsername = null
            };

            Assert.NotNull(state.GameLog);
            Assert.Equal(2, state.GameLog.Count);
            Assert.False(state.IsKicked);
            Assert.Equal("Player1", state.CurrentTurnUsername);
        }

        [Theory]
        [InlineData("Voter1", "Target1", "Toxic behavior")]
        [InlineData("Voter2", "Target2", "AFK")]
        [InlineData("P1", "P2", "Spam")]
        [InlineData("P3", "P4", "Inactivity")]
        [InlineData("Admin", "User", "Rules violation")]
        [InlineData("User1", "User2", "Cheating")]
        [InlineData("A", "B", "Reason")]
        [InlineData("X", "Y", "Z")]
        [InlineData("Player", "Enemy", "Griefing")]
        [InlineData("Me", "You", "None")]
        public void VoteRequestDto_Integrity(string user, string target, string reason)
        {
            var dto = new VoteRequestDto { Username = user, TargetUsername = target, Reason = reason };
            Assert.Equal(user, dto.Username);
            Assert.Equal(target, dto.TargetUsername);
            Assert.Equal(reason, dto.Reason);
        }
    }
}