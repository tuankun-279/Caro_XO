using Caro.AI;
using Caro.Core;
using UnityEngine;

namespace Caro.Game
{
    /// <summary>Remembered choices for games against the AI (shared by the mode-selection and settings screens).</summary>
    public static class AiPrefs
    {
        private const string KeyLastDifficulty = "caro.ai.lastDifficulty";
        private const string KeyLastHuman = "caro.ai.lastHuman";

        /// <summary>Difficulty used for the next game against the AI.</summary>
        public static Difficulty LastDifficulty
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(KeyLastDifficulty, (int)Difficulty.Medium), 0, 2);
            set { PlayerPrefs.SetInt(KeyLastDifficulty, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>The side the player takes against the AI (X moves first).</summary>
        public static Piece LastHuman
        {
            get => PlayerPrefs.GetInt(KeyLastHuman, (int)Piece.X) == (int)Piece.O ? Piece.O : Piece.X;
            set { PlayerPrefs.SetInt(KeyLastHuman, (int)value); PlayerPrefs.Save(); }
        }
    }
}
