using GameServer.DTOs.Gameplay;
using GameServer.Models;
using System;
using System.Linq;

namespace GameServer.GameEngines
{
    public class GooseBoardEngine
    {
        private static readonly int[] GooseTiles = { 5, 9, 18, 23, 27, 32, 36, 41, 45, 50, 54, 59 };
        private static readonly int[] LuckyBoxTiles = { 7, 14, 25, 34 };
        private static readonly Random RandomGenerator = new Random();
        private static readonly object _randomLock = new object();

        public class BoardMoveResult
        {
            public int FinalPosition { get; set; }
            public string Message { get; set; }
            public bool IsExtraTurn { get; set; }
            public int TurnsToSkip { get; set; }
            public string LuckyBoxTag { get; set; }
            public RewardResult Reward { get; set; }
        }

        public (int D1, int D2) GenerateDiceRoll(int currentPos)
        {
            int d1, d2;
            lock (_randomLock)
            {
                d1 = RandomGenerator.Next(1, 7);
                d2 = (currentPos < 60) ? RandomGenerator.Next(1, 7) : 0;
            }
            return (d1, d2);
        }

        public BoardMoveResult CalculateBoardRules(int initialPos, string username, ref int playerCoins, ref int ticketCommon, ref int ticketEpic, ref int ticketLegendary)
        {
            var result = new BoardMoveResult
            {
                FinalPosition = initialPos,
                IsExtraTurn = false,
                TurnsToSkip = 0,
                Message = string.Empty,
                LuckyBoxTag = string.Empty
            };

            if (result.FinalPosition > 64)
            {
                result.FinalPosition = 64 - (result.FinalPosition - 64);
            }

            if (result.FinalPosition == 64)
            {
                result.Message = "WIN";
            }
            else
            {
                ApplyTileRules(result, username, ref playerCoins, ref ticketCommon, ref ticketEpic, ref ticketLegendary);
            }

            return result;
        }

        private void ApplyTileRules(BoardMoveResult result, string username, ref int coins, ref int tCommon, ref int tEpic, ref int tLegendary)
        {
            if (LuckyBoxTiles.Contains(result.FinalPosition))
            {
                var reward = ProcessLuckyBoxReward(ref coins, ref tCommon, ref tEpic, ref tLegendary);
                result.Reward = reward;
                result.LuckyBoxTag = string.Format("[LUCKYBOX:{0}:{1}_{2}]", username, reward.Type, reward.Amount);
                result.Message = string.Format("¡CAJA DE LA SUERTE! {0}", reward.Description);
            }
            else if (GooseTiles.Contains(result.FinalPosition))
            {
                HandleGooseRule(result);
            }
            else if (result.FinalPosition == 6 || result.FinalPosition == 12)
            {
                HandleBridgeRule(result);
            }
            else
            {
                HandlePenaltyRules(result);
            }
        }

        private static void HandleGooseRule(BoardMoveResult result)
        {
            int nextGoose = GooseTiles.FirstOrDefault(t => t > result.FinalPosition);
            if (nextGoose != 0)
            {
                result.Message = string.Format("¡De Oca a Oca ({0} -> {1})! Tira de nuevo.", result.FinalPosition, nextGoose);
                result.FinalPosition = nextGoose;
            }
            else
            {
                result.Message = "¡Oca (59)! Tira de nuevo.";
            }
            result.IsExtraTurn = true;
        }

        private static void HandleBridgeRule(BoardMoveResult result)
        {
            if (result.FinalPosition == 6)
            {
                result.Message = "¡De Puente a Puente! Saltas al 12 y tiras de nuevo.";
                result.FinalPosition = 12;
            }
            else
            {
                result.Message = "¡De Puente a Puente! Regresas al 6 y tiras de nuevo.";
                result.FinalPosition = 6;
            }
            result.IsExtraTurn = true;
        }

        private static void HandlePenaltyRules(BoardMoveResult result)
        {
            switch (result.FinalPosition)
            {
                case 42:
                    result.Message = "¡Laberinto! Retrocedes a la 30.";
                    result.FinalPosition = 30;
                    break;
                case 58:
                    result.Message = "¡CALAVERA! Regresas al inicio (1).";
                    result.FinalPosition = 1;
                    break;
                case 26:
                case 53:
                    int bonus = result.FinalPosition;
                    result.Message = string.Format("¡Dados! Sumas {0} casillas extra.", bonus);
                    result.FinalPosition += bonus;
                    if (result.FinalPosition > 64) result.FinalPosition = 64 - (result.FinalPosition - 64);
                    break;
                case 19:
                    result.Message = "¡Posada! Pierdes 1 turno.";
                    result.TurnsToSkip = 1;
                    break;
                case 31:
                    result.Message = "¡Pozo! Esperas rescate (2 turnos).";
                    result.TurnsToSkip = 2;
                    break;
                case 56:
                    result.Message = "¡Cárcel! Esperas 3 turnos.";
                    result.TurnsToSkip = 3;
                    break;
            }
        }

        private RewardResult ProcessLuckyBoxReward(ref int coins, ref int tCommon, ref int tEpic, ref int tLegendary)
        {
            int roll;
            lock (_randomLock)
            {
                roll = RandomGenerator.Next(1, 101);
            }

            RewardResult reward;

            if (roll <= 50)
            {
                int amount = 50;
                coins += amount;
                reward = new RewardResult { Type = "COINS", Amount = amount, Description = string.Format("¡Has encontrado {0} Monedas de Oro!", amount) };
            }
            else if (roll <= 80)
            {
                tCommon++;
                reward = new RewardResult { Type = "COMMON", Amount = 1, Description = "¡Has desbloqueado un Ticket COMÚN!" };
            }
            else if (roll <= 95)
            {
                tEpic++;
                reward = new RewardResult { Type = "EPIC", Amount = 1, Description = "¡INCREÍBLE! ¡Ticket ÉPICO obtenido!" };
            }
            else
            {
                tLegendary++;
                reward = new RewardResult { Type = "LEGENDARY", Amount = 1, Description = "¡JACKPOT! ¡Ticket LEGENDARIO!" };
            }

            return reward;
        }

        public string BuildActionDescription(string username, int d1, int d2, BoardMoveResult rule)
        {
            string baseMsg = d2 > 0 ? string.Format("{0} tiró {1} y {2}.", username, d1, d2) : string.Format("{0} tiró {1}.", username, d1);
            string fullDescription = string.IsNullOrEmpty(rule.Message)
                ? string.Format("{0} Avanza a {1}.", baseMsg, rule.FinalPosition)
                : string.Format("{0} {1}", baseMsg, rule.Message);

            if (rule.IsExtraTurn) fullDescription = "[EXTRA] " + fullDescription;
            if (!string.IsNullOrEmpty(rule.LuckyBoxTag)) fullDescription = rule.LuckyBoxTag + " " + fullDescription;

            return fullDescription;
        }
    }
}