using GameServer.DTOs.Lobby;
using System.Collections.Generic;
using Xunit;

namespace GameServer.Tests.DTOs
{
    public class LobbyDtoTests
    {
        [Theory]
        [InlineData("GANS01", "Player1", 1, 2, 4)]
        [InlineData("LOBB99", "HostMaster", 2, 4, 4)]
        [InlineData("FREE00", "Guest", 1, 1, 2)]
        [InlineData("NULLID", "", 0, 0, 0)]
        [InlineData("X", "Y", 99, 10, 10)]
        [InlineData("MAXCAP", "Admin", 3, 8, 8)]
        [InlineData("JOINME", "User2", 1, 3, 6)]
        [InlineData("PARTID", "Ganso", 1, 2, 2)]
        [InlineData("FULLHOUSE", "Owner", 2, 4, 4)]
        [InlineData("TEST_1", "TestHost", 1, 1, 4)]
        public void ActiveMatchDto_Integrity(string code, string host, int board, int current, int max)
        {
            var dto = new ActiveMatchDto { LobbyCode = code, HostUsername = host, BoardId = board, CurrentPlayers = current, MaxPlayers = max };
            Assert.Equal(code, dto.LobbyCode);
            Assert.Equal(host, dto.HostUsername);
            Assert.Equal(board, dto.BoardId);
            Assert.Equal(current, dto.CurrentPlayers);
            Assert.Equal(max, dto.MaxPlayers);
        }

        [Theory]
        [InlineData("ROOM12", "UserA")]
        [InlineData("SALA55", "UserB")]
        [InlineData("G00S3", "Gansito")]
        [InlineData("J01N", "PlayerX")]
        [InlineData("123456", "Numero")]
        [InlineData("", "")]
        [InlineData(null, null)]
        [InlineData("B4TTL3", "Soldier")]
        [InlineData("QUICK", "FastPlayer")]
        [InlineData("LAST", "TheEnd")]
        public void JoinLobbyRequest_Integrity(string code, string user)
        {
            var dto = new JoinLobbyRequest { LobbyCode = code, Username = user };
            Assert.Equal(code, dto.LobbyCode);
            Assert.Equal(user, dto.Username);
        }

        [Theory]
        [InlineData("LOBBY1", "TargetPlayer", "HostPlayer")]
        [InlineData("CODE2", "Cheater", "Admin")]
        [InlineData("GAMES", "Noob", "Pro")]
        [InlineData("123", "User", "Owner")]
        [InlineData("ABC", "X", "Y")]
        [InlineData("", "", "")]
        [InlineData("TEST", "Toxic", "Moderator")]
        [InlineData("SALA", "Bot", "System")]
        [InlineData("EXIT", "Player", "Host")]
        [InlineData("KICK", "Guest", "Root")]
        public void KickPlayerRequest_Integrity(string code, string target, string requestor)
        {
            var dto = new KickPlayerRequest { LobbyCode = code, TargetUsername = target, RequestorUsername = requestor };
            Assert.Equal(code, dto.LobbyCode);
            Assert.Equal(target, dto.TargetUsername);
            Assert.Equal(requestor, dto.RequestorUsername);
        }

        [Theory]
        [InlineData(true, "LOBBY_A", null)]
        [InlineData(false, null, "Lobby Full")]
        [InlineData(true, "GANSITO", "")]
        [InlineData(false, "", "Invalid Settings")]
        [InlineData(true, "123456", "Ok")]
        [InlineData(false, "ERROR", "Server Timeout")]
        [InlineData(true, "ABCDEF", "Success")]
        [InlineData(false, null, "Board not found")]
        [InlineData(true, "L00P", "Ready")]
        [InlineData(false, "FAIL", "Banned from creating")]
        public void LobbyCreationResultDto_Integrity(bool success, string code, string error)
        {
            var dto = new LobbyCreationResultDto { Success = success, LobbyCode = code, ErrorMessage = error };
            Assert.Equal(success, dto.Success);
            Assert.Equal(code, dto.LobbyCode);
            Assert.Equal(error, dto.ErrorMessage);
        }

        [Theory]
        [InlineData(true, 4, 1)]
        [InlineData(false, 2, 2)]
        [InlineData(true, 6, 1)]
        [InlineData(false, 3, 1)]
        [InlineData(true, 8, 3)]
        [InlineData(false, 4, 2)]
        [InlineData(true, 2, 1)]
        [InlineData(false, 6, 3)]
        [InlineData(true, 4, 2)]
        [InlineData(true, 4, 1)]
        public void LobbySettingsDto_Integrity(bool isPublic, int max, int board)
        {
            var dto = new LobbySettingsDto { IsPublic = isPublic, MaxPlayers = max, BoardId = board };
            Assert.Equal(isPublic, dto.IsPublic);
            Assert.Equal(max, dto.MaxPlayers);
            Assert.Equal(board, dto.BoardId);
        }

        [Fact]
        public void JoinLobbyResultDto_Initialization_VerifyList()
        {
            var dto = new JoinLobbyResultDto();
            Assert.NotNull(dto.PlayersInLobby);
            Assert.Empty(dto.PlayersInLobby);

            dto.PlayersInLobby.Add(new PlayerLobbyDto { Username = "Host", IsHost = true });
            Assert.Single(dto.PlayersInLobby);
        }
    }
}