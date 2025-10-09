using System;
using System.Linq;
using System.ServiceModel;
using BCrypt.Net;
namespace GameServer
{
    public class GameService : IGameService
    {
        public bool RegistrarUsuario(string username, string email, string password)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    if (context.Players.Any(p => p.Username == username) || context.Accounts.Any(a => a.Email == email))
                    {
                        return false;
                    }

                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                    var nuevaCuenta = new Account
                    {
                        Email = email,
                        PasswordHash = hashedPassword,
                        RegisterDate = DateTime.Now,
                        AccountStatus = (int)AccountStatus.Active

                    };

                    // creamos la entidad de estadísticas con valores iniciales
                    var nuevasEstadisticas = new PlayerStat
                    {
                        MatchesPlayed = 0,
                        MatchesWon = 0,
                        MatchesLost = 0,
                        LuckyBoxOpened = 0
                    };

                    // creamos al jugador y le ASOCIAMOS sus estadísticas
                    var nuevoJugador = new Player
                    {
                        Username = username,
                        Coins = 0,
                        Avatar = "default_avatar.png",
                        Account = nuevaCuenta,
                        PlayerStat = nuevasEstadisticas 
                    };

                    context.Players.Add(nuevoJugador);
                    context.SaveChanges();

                    Console.WriteLine($"Usuario '{username}' registrado exitosamente.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RegistrarUsuario: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public bool IniciarSesion(string username, string password)
        {
            try
            {
                using (var context = new GameDatabase_Container())
                {
                    var jugador = context.Players.FirstOrDefault(p => p.Username == username);

                    if (jugador == null)
                    {
                        return false;
                    }

                    if (BCrypt.Net.BCrypt.Verify(password, jugador.Account.PasswordHash))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en IniciarSesion: {ex.Message}");
                return false;
            }
        }

        public bool CambiarContrasena(string username, string oldPassword, string newPassword)
        {
            throw new NotImplementedException();
        }
    }
}