using System;

namespace GameServer.Services.Common
{

    public interface ISecurityCodeGenerator
    {
        int Next(int minValue, int maxValue);
    }

    public class SecurityCodeGenerator : ISecurityCodeGenerator
    {
        private static readonly Random _random = new Random();

        public int Next(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }
    }

    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool Verify(string text, string hash);
    }

    public class PasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool Verify(string text, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(text, hash);
        }
    }
}