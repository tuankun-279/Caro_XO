using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caro.Game
{
    public enum Language { English = 0, Vietnamese = 1 }

    /// <summary>
    /// The game's two languages. Texts are written in Vietnamese in the code and the scene, and that Vietnamese
    /// text is the key into the English table below, so a screen stays readable in the source and a missing
    /// translation simply shows the Vietnamese text. English is the default on first launch.
    /// Static labels use <see cref="LocalizedText"/>; texts built at run time call <see cref="T"/> or <see cref="F"/>.
    /// </summary>
    public static class Loc
    {
        private const string KeyLanguage = "caro.settings.language";

        /// <summary>Raised after the language changes so every visible text can be redrawn.</summary>
        public static event Action Changed;

        private static Language? _current;

        public static Language Current
        {
            get
            {
                if (_current == null)
                    _current = PlayerPrefs.GetInt(KeyLanguage, (int)Language.English) == (int)Language.Vietnamese
                        ? Language.Vietnamese : Language.English;
                return _current.Value;
            }
            set
            {
                if (value == Current) return;
                _current = value;
                PlayerPrefs.SetInt(KeyLanguage, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Name of each language written in that language, for the language picker.</summary>
        public static string NativeName(Language l) => l == Language.Vietnamese ? "Tiếng Việt" : "English";

        /// <summary>True when the text has an English translation (used by the UI builder).</summary>
        public static bool Has(string vietnamese) => vietnamese != null && English.ContainsKey(vietnamese);

        /// <summary>The text in the current language.</summary>
        public static string T(string vietnamese)
        {
            if (string.IsNullOrEmpty(vietnamese) || Current == Language.Vietnamese) return vietnamese;
            if (English.TryGetValue(vietnamese, out var en)) return en;
#if UNITY_EDITOR
            Debug.LogWarning($"[Loc] Missing English text for \"{vietnamese}\"");
#endif
            return vietnamese;
        }

        /// <summary>The text in the current language with {0}, {1}… filled in.</summary>
        public static string F(string vietnamese, params object[] args) => string.Format(T(vietnamese), args);

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // Menu
            { "Nối 5 quân là thắng!", "Five in a row wins!" },
            { "Chơi với máy", "Play vs Bot" },
            { "Chơi 2 người", "2 Players" },
            { "Cài đặt", "Settings" },
            { "{0} · đang dở", "{0} · in progress" },
            { "Tỉ số gần đây", "Recent scores" },
            { "Máy – Dễ", "Bot – Easy" },
            { "Máy – Vừa", "Bot – Medium" },
            { "Máy – Khó", "Bot – Hard" },
            { "2 người", "2 players" },

            // Mode selection
            { "Chọn chế độ chơi", "Choose a mode" },
            { "Thử thách với 3 mức độ khó", "Take on 3 difficulty levels" },
            { "Chơi cùng bạn trên\ncùng một thiết bị", "Play with a friend\non the same device" },
            { "Độ khó <size=80%>(khi chơi với máy)</size>", "Difficulty <size=80%>(vs Bot)</size>" },
            { "Dễ", "Easy" },
            { "Vừa", "Medium" },
            { "Khó", "Hard" },
            { "Chơi tiếp ván dở", "Resume game" },
            { "Chơi tiếp ván dở  ·  {0}, bạn cầm {1}", "Resume  ·  {0}, you play {1}" },
            { "Chơi tiếp ván 2 người đang dở", "Resume 2-player game" },
            { "Bắt đầu", "Start" },

            // Game
            { "Chơi với máy - {0}", "Vs Bot - {0}" },
            { "Vừa màn hình", "Fit screen" },
            { "Đi lại", "Undo" },
            { "Ván mới", "New game" },
            { "Tỉ số", "Score" },
            { "Máy đang nghĩ… {0}", "Bot is thinking… {0}" },
            { "Lượt của bạn {0}", "Your turn {0}" },
            { "Lượt của người chơi {0}", "Player {0} to move" },
            { "Hoà! Bàn cờ đã kín", "Draw! The board is full" },
            { "Bạn thắng!", "You win!" },
            { "Máy thắng!", "Bot wins!" },
            { "Người chơi {0} thắng!", "Player {0} wins!" },
            { "Bàn {0} × {1}  ·  tối đa {2} × {2}", "Board {0} × {1}  ·  max {2} × {2}" },
            { "Bạn", "You" },
            { "Máy", "Bot" },
            { "Người chơi", "Player" },

            // Result
            { "Hoà!", "Draw!" },
            { "Bàn cờ đã kín", "The board is full" },
            { "<color={0}>Bạn</color> <color={1}>thắng!</color>", "<color={0}>You</color> <color={1}>win!</color>" },
            { "<color={0}>Máy</color> <color={1}>thắng!</color>", "<color={0}>Bot</color> <color={1}>wins!</color>" },
            { "<color={0}>Người chơi {2}</color> <color={1}>thắng!</color>", "<color={0}>Player {2}</color> <color={1}>wins!</color>" },
            { "Nối 5 quân liền thắng!", "Five in a row!" },
            { "Thử lại nhé!", "Try again!" },
            { "Chơi lại", "Play again" },
            { "Về menu", "Menu" },
            { "Đổi độ khó", "Change difficulty" },
            { "<u>Xem lại bàn cờ</u>", "<u>Review the board</u>" },

            // Settings
            { "Ngôn ngữ", "Language" },
            { "Âm thanh", "Sound" },
            { "Bật âm thanh", "Sound effects" },
            { "Rung", "Vibration" },
            { "Bật", "On" },
            { "Tắt", "Off" },
            { "Ai đi trước <size=80%>(chơi với máy)</size>", "Who goes first <size=80%>(vs Bot)</size>" },
            { "Đổi sẽ bắt đầu ván mới · tỉ số giữ nguyên", "Changing starts a new game · score is kept" },
            { "Áp dụng cho ván đấu máy tiếp theo", "Applies to your next game vs Bot" },
            { "Độ khó <size=80%>(chơi với máy)</size>", "Difficulty <size=80%>(vs Bot)</size>" },
            { "Giao diện", "Theme" },
            { "Mặc định", "Default" },
            { "Giấy ô li", "Grid paper" },
            { "Tối giản", "Minimal" },
        };
    }
}
