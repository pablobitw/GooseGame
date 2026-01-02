using Xunit;
using Moq;
using GameServer.Helpers;
using GameServer.Interfaces;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace GameServer.Tests.Unit
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
            var type = typeof(ConnectionManager);

            var usersField = type.GetField("_activeUsers", BindingFlags.Static | BindingFlags.NonPublic);
            var users = (HashSet<string>)usersField.GetValue(null);
            users.Clear();

            var lobbyField = type.GetField("_lobbyCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
            var lobbyDict = (Dictionary<string, ILobbyServiceCallback>)lobbyField.GetValue(null);
            lobbyDict.Clear();

            var gameField = type.GetField("_gameplayCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
            var gameDict = (Dictionary<string, IGameplayServiceCallback>)gameField.GetValue(null);
            gameDict.Clear();
        }


        [Fact]
        public void AddUser_UsuarioNuevo_RetornaTrue()
        {
            bool result = ConnectionManager.AddUser("User1");
            Assert.True(result);
        }

        [Fact]
        public void AddUser_UsuarioNuevo_SeAgregaALista()
        {
            ConnectionManager.AddUser("User1");
            Assert.True(ConnectionManager.IsUserOnline("User1"));
        }

        [Fact]
        public void AddUser_UsuarioExistente_RetornaFalse()
        {
            ConnectionManager.AddUser("User1");
            bool result = ConnectionManager.AddUser("User1");
            Assert.False(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddUser_UsuarioInvalido_RetornaFalse(string username)
        {
            bool result = ConnectionManager.AddUser(username);
            Assert.False(result);
        }



        [Fact]
        public void RemoveUser_UsuarioExistente_LoEliminaDeActivos()
        {
            ConnectionManager.AddUser("User1");
            ConnectionManager.RemoveUser("User1");
            Assert.False(ConnectionManager.IsUserOnline("User1"));
        }

        [Fact]
        public void RemoveUser_UsuarioConCallbacks_LimpiaLobbyCallback()
        {
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.AddUser("User1");
            ConnectionManager.RegisterLobbyClient("User1", mockCallback.Object);

            ConnectionManager.RemoveUser("User1");

            var result = ConnectionManager.GetLobbyClient("User1");
            Assert.Null(result);
        }

        [Fact]
        public void RemoveUser_UsuarioConCallbacks_LimpiaGameplayCallback()
        {
            var mockCallback = new Mock<IGameplayServiceCallback>();
            ConnectionManager.AddUser("User1");
            ConnectionManager.RegisterGameplayClient("User1", mockCallback.Object);

            ConnectionManager.RemoveUser("User1");

            var result = ConnectionManager.GetGameplayClient("User1");
            Assert.Null(result);
        }

    

        [Fact]
        public void RegisterLobby_NuevoRegistro_LoGuardaCorrectamente()
        {
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient("User1", mockCallback.Object);

            var retrieved = ConnectionManager.GetLobbyClient("User1");
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void RegisterLobby_UsuarioExistente_ActualizaElCallback()
        {
            var mockCallback1 = new Mock<ILobbyServiceCallback>();
            var mockCallback2 = new Mock<ILobbyServiceCallback>();

            ConnectionManager.RegisterLobbyClient("User1", mockCallback1.Object);
            ConnectionManager.RegisterLobbyClient("User1", mockCallback2.Object);

            var retrieved = ConnectionManager.GetLobbyClient("User1");
            Assert.Same(mockCallback2.Object, retrieved);
        }

        [Fact]
        public void UnregisterLobby_UsuarioExistente_LoElimina()
        {
            var mockCallback = new Mock<ILobbyServiceCallback>();
            ConnectionManager.RegisterLobbyClient("User1", mockCallback.Object);

            ConnectionManager.UnregisterLobbyClient("User1");

            var result = ConnectionManager.GetLobbyClient("User1");
            Assert.Null(result);
        }

 

        [Fact]
        public void RegisterGameplay_NuevoRegistro_LoGuardaCorrectamente()
        {
            var mockCallback = new Mock<IGameplayServiceCallback>();
            ConnectionManager.RegisterGameplayClient("Gamer1", mockCallback.Object);

            var retrieved = ConnectionManager.GetGameplayClient("Gamer1");
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void UnregisterGameplay_UsuarioExistente_LoElimina()
        {
            var mockCallback = new Mock<IGameplayServiceCallback>();
            ConnectionManager.RegisterGameplayClient("Gamer1", mockCallback.Object);

            ConnectionManager.UnregisterGameplayClient("Gamer1");

            var result = ConnectionManager.GetGameplayClient("Gamer1");
            Assert.Null(result);
        }
    }
}