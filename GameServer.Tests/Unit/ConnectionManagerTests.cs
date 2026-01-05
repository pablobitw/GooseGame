using GameServer.Helpers;
using GameServer.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace GameServer.Tests.Helpers
{
    public class ConnectionManagerTests : IDisposable
    {
        public ConnectionManagerTests()
        {
            ResetStaticData();
        }

        public void Dispose()
        {
            ResetStaticData();
        }

        private void ResetStaticData()
        {
            var activeUsersField = typeof(ConnectionManager).GetField("_activeUsers", BindingFlags.NonPublic | BindingFlags.Static);
            var activeUsers = (HashSet<string>)activeUsersField.GetValue(null);
            activeUsers.Clear();

            var lobbyField = typeof(ConnectionManager).GetField("_lobbyCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
            var lobbyCallbacks = (Dictionary<string, ILobbyServiceCallback>)lobbyField.GetValue(null);
            lobbyCallbacks.Clear();

            var gameplayField = typeof(ConnectionManager).GetField("_gameplayCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
            var gameplayCallbacks = (Dictionary<string, IGameplayServiceCallback>)gameplayField.GetValue(null);
            gameplayCallbacks.Clear();
        }


        [Fact]
        public void AddUser_NewUser_ReturnsTrue()
        {
            // Act
            bool result = ConnectionManager.AddUser("User1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AddUser_ExistingUser_ReturnsFalse()
        {
            // Arrange
            ConnectionManager.AddUser("User1");

            // Act
            bool result = ConnectionManager.AddUser("User1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddUser_DifferentCase_ReturnsFalse()
        {
            ConnectionManager.AddUser("User1");

            // Act
            bool result = ConnectionManager.AddUser("USER1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddUser_NullUsername_ReturnsFalse()
        {
            // Act
            bool result = ConnectionManager.AddUser(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddUser_WhitespaceUsername_ReturnsFalse()
        {
            // Act
            bool result = ConnectionManager.AddUser("   ");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsUserOnline_UserAdded_ReturnsTrue()
        {
            // Arrange
            ConnectionManager.AddUser("UserOnline");

            // Act
            bool result = ConnectionManager.IsUserOnline("UserOnline");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsUserOnline_UserNotAdded_ReturnsFalse()
        {
            // Act
            bool result = ConnectionManager.IsUserOnline("GhostUser");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsUserOnline_NullInput_ReturnsFalse()
        {
            // Act
            bool result = ConnectionManager.IsUserOnline(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveUser_ExistingUser_RemovesFromActiveSet()
        {
            // Arrange
            ConnectionManager.AddUser("UserToRemove");

            // Act
            ConnectionManager.RemoveUser("UserToRemove");

            // Assert
            Assert.False(ConnectionManager.IsUserOnline("UserToRemove"));
        }

        [Fact]
        public void RemoveUser_ExistingUser_RemovesCallbacksToo()
        {
            // Arrange
            string user = "UserFull";
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.AddUser(user);
            ConnectionManager.RegisterLobbyClient(user, mockCallback.Object);

            // Act
            ConnectionManager.RemoveUser(user);

            // Assert
            Assert.Null(ConnectionManager.GetLobbyClient(user));
        }

        [Fact]
        public void RemoveUser_NullInput_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => ConnectionManager.RemoveUser(null));
            Assert.Null(exception);
        }



        [Fact]
        public void RegisterLobbyClient_NewUser_StoresCallback()
        {
            // Arrange
            string user = "LobbyUser";
            var mockCallback = new Mock<ILobbyServiceCallback>();

            // Act
            ConnectionManager.RegisterLobbyClient(user, mockCallback.Object);

            // Assert
            Assert.Same(mockCallback.Object, ConnectionManager.GetLobbyClient(user));
        }

        [Fact]
        public void RegisterLobbyClient_ExistingUser_OverwritesCallback()
        {
            // Arrange
            string user = "LobbyUser";
            var mockCallback1 = new Mock<ILobbyServiceCallback>();
            var mockCallback2 = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient(user, mockCallback1.Object);

            // Act
            ConnectionManager.RegisterLobbyClient(user, mockCallback2.Object);

            // Assert
            Assert.Same(mockCallback2.Object, ConnectionManager.GetLobbyClient(user));
        }

        [Fact]
        public void GetLobbyClient_UserNotFound_ReturnsNull()
        {
            // Act
            var result = ConnectionManager.GetLobbyClient("Unknown");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void UnregisterLobbyClient_ExistingUser_RemovesCallback()
        {
            // Arrange
            string user = "UserToUnreg";
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient(user, mockCallback.Object);

            // Act
            ConnectionManager.UnregisterLobbyClient(user);

            // Assert
            Assert.Null(ConnectionManager.GetLobbyClient(user));
        }

        [Fact]
        public void UnregisterLobbyClient_NonExisting_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => ConnectionManager.UnregisterLobbyClient("Ghost"));
            Assert.Null(exception);
        }


        [Fact]
        public void RegisterGameplayClient_NewUser_StoresCallback()
        {
            // Arrange
            string user = "GameUser";
            var mockCallback = new Mock<IGameplayServiceCallback>();

            // Act
            ConnectionManager.RegisterGameplayClient(user, mockCallback.Object);

            // Assert
            Assert.Same(mockCallback.Object, ConnectionManager.GetGameplayClient(user));
        }

        [Fact]
        public void RegisterGameplayClient_ExistingUser_OverwritesCallback()
        {
            // Arrange
            string user = "GameUser";
            var mockCallback1 = new Mock<IGameplayServiceCallback>();
            var mockCallback2 = new Mock<IGameplayServiceCallback>();
            ConnectionManager.RegisterGameplayClient(user, mockCallback1.Object);

            // Act
            ConnectionManager.RegisterGameplayClient(user, mockCallback2.Object);

            // Assert
            Assert.Same(mockCallback2.Object, ConnectionManager.GetGameplayClient(user));
        }

        [Fact]
        public void GetGameplayClient_UserNotFound_ReturnsNull()
        {
            // Act
            var result = ConnectionManager.GetGameplayClient("Unknown");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void UnregisterGameplayClient_ExistingUser_RemovesCallback()
        {
            // Arrange
            string user = "UserToUnreg";
            var mockCallback = new Mock<IGameplayServiceCallback>();
            ConnectionManager.RegisterGameplayClient(user, mockCallback.Object);

            // Act
            ConnectionManager.UnregisterGameplayClient(user);

            // Assert
            Assert.Null(ConnectionManager.GetGameplayClient(user));
        }

        [Fact]
        public void UnregisterGameplayClient_NonExisting_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => ConnectionManager.UnregisterGameplayClient("Ghost"));
            Assert.Null(exception);
        }
    }
}