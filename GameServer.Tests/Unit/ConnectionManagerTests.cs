using GameServer.Helpers;
using GameServer.Interfaces;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace GameServer.Tests.Helpers
{
    public class ConnectionManagerTests : IDisposable
    {
        public ConnectionManagerTests()
        {
            ResetStaticState();
        }

        public void Dispose()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var activeUsersField = typeof(ConnectionManager).GetField("_activeUsers", BindingFlags.NonPublic | BindingFlags.Static);
            var lobbyCallbacksField = typeof(ConnectionManager).GetField("_lobbyCallbacks", BindingFlags.NonPublic | BindingFlags.Static);
            var gameplayCallbacksField = typeof(ConnectionManager).GetField("_gameplayCallbacks", BindingFlags.NonPublic | BindingFlags.Static);

            if (activeUsersField != null)
            {
                var hashSet = activeUsersField.GetValue(null);
                hashSet?.GetType().GetMethod("Clear").Invoke(hashSet, null);
            }

            if (lobbyCallbacksField != null)
            {
                var dict = (IDictionary)lobbyCallbacksField.GetValue(null);
                dict?.Clear();
            }

            if (gameplayCallbacksField != null)
            {
                var dict = (IDictionary)gameplayCallbacksField.GetValue(null);
                dict?.Clear();
            }
        }

        [Fact]
        public void AddUser_NewUser_ShouldReturnTrue()
        {
            string username = "PlayerOne";

            bool result = ConnectionManager.AddUser(username);

            Assert.True(result);
        }

        [Fact]
        public void AddUser_DuplicateUser_ShouldReturnFalse()
        {
            string username = "PlayerOne";
            ConnectionManager.AddUser(username);

            bool result = ConnectionManager.AddUser(username);

            Assert.False(result);
        }

        [Fact]
        public void AddUser_NullUsername_ShouldReturnFalse()
        {
            string username = null;

            bool result = ConnectionManager.AddUser(username);

            Assert.False(result);
        }

        [Fact]
        public void AddUser_EmptyUsername_ShouldReturnFalse()
        {
            string username = "   ";

            bool result = ConnectionManager.AddUser(username);

            Assert.False(result);
        }

        [Fact]
        public void IsUserOnline_UserExists_ShouldReturnTrue()
        {
            string username = "OnlineUser";
            ConnectionManager.AddUser(username);

            bool result = ConnectionManager.IsUserOnline(username);

            Assert.True(result);
        }

        [Fact]
        public void IsUserOnline_UserDoesNotExist_ShouldReturnFalse()
        {
            bool result = ConnectionManager.IsUserOnline("GhostUser");

            Assert.False(result);
        }

        [Fact]
        public void RegisterLobbyClient_NewClient_ShouldStoreCallback()
        {
            string username = "LobbyUser";
            var mockCallback = new Mock<ILobbyServiceCallback>();

            ConnectionManager.RegisterLobbyClient(username, mockCallback.Object);
            var retrieved = ConnectionManager.GetLobbyClient(username);

            Assert.NotNull(retrieved);
        }

        [Fact]
        public void RegisterLobbyClient_ExistingClient_ShouldUpdateCallback()
        {
            string username = "LobbyUser";
            var mockCallback1 = new Mock<ILobbyServiceCallback>();
            var mockCallback2 = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient(username, mockCallback1.Object);

            ConnectionManager.RegisterLobbyClient(username, mockCallback2.Object);
            var retrieved = ConnectionManager.GetLobbyClient(username);

            Assert.Same(mockCallback2.Object, retrieved);
        }

        [Fact]
        public void UnregisterLobbyClient_UserExists_ShouldRemoveCallback()
        {
            string username = "UserToRemove";
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient(username, mockCallback.Object);

            ConnectionManager.UnregisterLobbyClient(username);
            var retrieved = ConnectionManager.GetLobbyClient(username);

            Assert.Null(retrieved);
        }

        [Fact]
        public void RegisterGameplayClient_NewClient_ShouldStoreCallback()
        {
            string username = "GameUser";
            var mockCallback = new Mock<IGameplayServiceCallback>();

            ConnectionManager.RegisterGameplayClient(username, mockCallback.Object);
            var retrieved = ConnectionManager.GetGameplayClient(username);

            Assert.NotNull(retrieved);
        }

        [Fact]
        public void UnregisterGameplayClient_UserExists_ShouldRemoveCallback()
        {
            string username = "GameUser";
            var mockCallback = new Mock<IGameplayServiceCallback>();
            ConnectionManager.RegisterGameplayClient(username, mockCallback.Object);

            ConnectionManager.UnregisterGameplayClient(username);
            var retrieved = ConnectionManager.GetGameplayClient(username);

            Assert.Null(retrieved);
        }

        [Fact]
        public void RemoveUser_UserWithCallbacks_ShouldCleanAllLists()
        {
            string username = "FullUser";
            ConnectionManager.AddUser(username);
            ConnectionManager.RegisterLobbyClient(username, new Mock<ILobbyServiceCallback>().Object);
            ConnectionManager.RegisterGameplayClient(username, new Mock<IGameplayServiceCallback>().Object);

            ConnectionManager.RemoveUser(username);

            Assert.False(ConnectionManager.IsUserOnline(username));
        }

        [Fact]
        public void RemoveUser_UserWithCallbacks_ShouldRemoveLobbyCallback()
        {
            string username = "FullUser";
            ConnectionManager.AddUser(username);
            ConnectionManager.RegisterLobbyClient(username, new Mock<ILobbyServiceCallback>().Object);

            ConnectionManager.RemoveUser(username);

            Assert.Null(ConnectionManager.GetLobbyClient(username));
        }

        [Fact]
        public void RemoveUser_UserWithCallbacks_ShouldRemoveGameplayCallback()
        {
            string username = "FullUser";
            ConnectionManager.AddUser(username);
            ConnectionManager.RegisterGameplayClient(username, new Mock<IGameplayServiceCallback>().Object);

            ConnectionManager.RemoveUser(username);

            Assert.Null(ConnectionManager.GetGameplayClient(username));
        }

        [Fact]
        public void RemoveUser_NullUsername_ShouldDoNothing()
        {
            ConnectionManager.RemoveUser(null);

            Assert.True(true);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddUser_ConcurrentAccess_ShouldHandleLockingCorrectly()
        {
            int threadCount = 100;
            string baseName = "ThreadUser_";
            var tasks = new System.Threading.Tasks.Task[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int localI = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() => ConnectionManager.AddUser(baseName + localI));
            }
            await System.Threading.Tasks.Task.WhenAll(tasks);

            Assert.True(ConnectionManager.IsUserOnline(baseName + "0"));
        }
    }
}