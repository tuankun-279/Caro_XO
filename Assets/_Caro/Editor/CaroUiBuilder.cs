using Caro.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.EditorTools
{
    /// <summary>
    /// Builds the five screens of the redesign (menu, mode selection, game, result, settings) from the
    /// art set in Assets/_Caro/Art/UIv2 (designer deliveries "Caro_xo_optimized", "Caro_XO_Assets_Round2" to "Round4", cleaned and trimmed).
    /// Every visible UI element is an art asset or text; nothing is drawn in code. Positions are in
    /// mockup pixels on a 1080 × 1920 canvas, measured from the top-left corner (or from the bottom for
    /// bottom-anchored blocks). Re-running it replaces the previous build.
    /// </summary>
    public static class CaroUiBuilder
    {
        private const string ArtRoot = "Assets/_Caro/Art/UIv2";
        private const string Art = ArtRoot + "/";
        private const float W = 1080f, H = 1920f;

        // Ink colours of the mockups
        private static readonly Color Ink = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color Quiet = new Color(0.33f, 0.36f, 0.42f);
        private static readonly Color Red = new Color(0.82f, 0.20f, 0.20f);
        private static readonly Color Blue = new Color(0.12f, 0.30f, 0.76f);
        private static readonly Color Mint = new Color(0.804f, 0.922f, 0.867f);

        // 9-slice borders in source pixels (left, bottom, right, top); first matching prefix wins.
        private static readonly (string prefix, Vector4 border)[] Borders =
        {
            ("buttons/", new Vector4(70, 70, 70, 70)),
            ("controls/slider_track_", new Vector4(32, 0, 32, 0)),
            ("panels/notebook_note", new Vector4(100, 80, 60, 80)),
            ("panels/score_panel", new Vector4(80, 50, 50, 50)),
            ("panels/blank_notebook_panel_small", new Vector4(40, 40, 40, 40)),
            ("panels/blank_notebook_panel", new Vector4(44, 36, 36, 36)),
            ("panels/blank_paper_panel", new Vector4(30, 30, 30, 30)),
            ("panels/player_robot_score_banner_only", new Vector4(24, 24, 24, 24)),
            ("panels/mode_card_", new Vector4(110, 110, 110, 110)),
            ("backgrounds/bg_page_frame", new Vector4(120, 120, 120, 120)),
        };

        private static TMP_FontAsset _font;

        [MenuItem("Caro/Build UI (mockups)")]
        public static void Build()
        {
            PrepareArt();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Caro/Art/Fonts/Pangolin SDF.asset");
            var canvas = GameObject.Find("UI Canvas");
            var board = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            if (canvas == null || board == null) throw new System.Exception("UI Canvas or Board not found");

            // Remove the previous screens (old popups included)
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);

            var settings = BuildSettings(canvas.transform);
            var game = BuildGame(canvas.transform, board, settings);
            var mode = BuildModeSelect(canvas.transform);
            var menu = BuildMenu(canvas.transform, game, board, mode, settings);
            menu.transform.SetAsFirstSibling();
            settings.transform.SetAsLastSibling();

            mode.gameObject.SetActive(false);
            game.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Mint;

            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            Debug.Log("[Caro] UI built from mockups (UIv2 art)");
        }

        /// <summary>Import settings for every texture of the art set: UI sprite, no mipmaps, lossless, 9-slice borders.</summary>
        public static void PrepareArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                var rel = path.Substring(Art.Length);
                var border = Vector4.zero;
                foreach (var (prefix, b) in Borders)
                    if (rel.StartsWith(prefix)) { border = b; break; }

                bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                    || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed
                    || ti.spriteBorder != border || !ti.alphaIsTransparency || ti.maxTextureSize != 2048;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 2048;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.spriteBorder = border;
                ti.SaveAndReimport();
            }
        }

        // ================= Screens =================

        private static MenuScreen BuildMenu(Transform canvas, GameScreen game, BoardView board, ModeSelectScreen mode, SettingsScreen settings)
        {
            var root = MakeScreen(canvas, "MenuScreen");
            Background(root);
            var safe = Safe(root);

            Pic(safe, "Logo", "logo/logo_caro_xo_illustrated", 560, 420, 760);
            var tagline = Txt(safe, "Tagline", "Nối 5 quân là thắng!", 580, 690, 640, 90, 60, Ink, TextAlignmentOptions.Center);
            tagline.transform.localRotation = Quaternion.Euler(0, 0, 4);
            Pic(safe, "TaglineUnderline", "doodles/tagline_underline", 580, 750, 520).rectTransform.localRotation = Quaternion.Euler(0, 0, 4);

            var vsAi = Btn(safe, "VsAiButton", "yellow", 560, 920, 780, 150, "Chơi với máy", "icons/pencil_clean_edges", 105);
            var twoP = Btn(safe, "TwoPlayerButton", "blue", 560, 1100, 780, 150, "Chơi 2 người", "icons/two_players_clean_edges", 115);
            var sett = Btn(safe, "SettingsButton", "gray", 560, 1280, 780, 150, "Cài đặt", "icons/settings_gear_clean_edges", 100);

            // "Tỉ số gần đây": blank notebook panel + tape + title text
            Sliced(safe, "RecentScores", "panels/notebook_note", 360, 1640, 560, 400, 0);
            Pic(safe, "Tape", "decorations/tape/red_tape_small", 130, 1455, 130);
            Txt(safe, "RecentTitle", "Tỉ số gần đây", 360, 1500, 460, 70, 46, Ink, TextAlignmentOptions.Center);
            var rows = new[] { "Máy – Dễ", "Máy – Vừa", "Máy – Khó", "2 người" };
            var scores = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 1575 + i * 66;
                Txt(safe, "RecentLabel" + i, rows[i], 165, y, 290, 64, 38, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
                scores[i] = Txt(safe, "RecentScore" + i, "0 - 0", 590, y, 140, 64, 38, Ink, TextAlignmentOptions.MidlineRight, pivotRight: true);
            }

            Pic(safe, "YellowStar", "doodles/yellow_star", 830, 1560, 70, 10);
            Pic(safe, "Pencil", "doodles/pencil_large", 820, 1730, 110, 0);

            var menu = root.gameObject.AddComponent<MenuScreen>();
            Set(menu, "gameScreen", game);
            Set(menu, "board", board);
            Set(menu, "modeSelect", mode);
            Set(menu, "settings", settings);
            Set(menu, "vsAiButton", vsAi);
            Set(menu, "twoPlayerButton", twoP);
            Set(menu, "settingsButton", sett);
            Set(menu, "vsAiLabel", Label(vsAi));
            Set(menu, "twoPlayerLabel", Label(twoP));
            SetArray(menu, "recentScores", scores);
            return menu;
        }

        private static ModeSelectScreen BuildModeSelect(Transform canvas)
        {
            var root = MakeScreen(canvas, "ModeSelectScreen");
            Background(root);
            var safe = Safe(root);
            var back = Header(safe, "Chọn chế độ chơi");

            var aiCard = SlicedButton(safe, "AiCard", "panels/mode_card_yellow", 560, 560, 860, 360);
            PicLocal(aiCard.transform, "Robot", "icons/robot_clean_edges", 0, -70, 150);
            Txt(aiCard.transform, "Title", "Chơi với máy", 0, 40, 780, 90, 66, Ink, TextAlignmentOptions.Center, local: true);
            Txt(aiCard.transform, "Subtitle", "Thử thách với 3 mức độ khó", 0, 110, 780, 60, 40, Quiet, TextAlignmentOptions.Center, local: true);
            var aiCheck = PicLocal(aiCard.transform, "Check", "controls/checkbox_checked", 360, -130, 70);

            var twoCard = SlicedButton(safe, "TwoPlayerCard", "panels/mode_card_blue", 560, 1000, 860, 420);
            PicLocal(twoCard.transform, "TwoPlayers", "icons/two_players_clean_edges", 0, -100, 190);
            Txt(twoCard.transform, "Title", "Chơi 2 người", 0, 20, 780, 90, 66, Ink, TextAlignmentOptions.Center, local: true);
            Txt(twoCard.transform, "Subtitle", "Chơi cùng bạn trên\ncùng một thiết bị", 0, 115, 780, 120, 40, Quiet, TextAlignmentOptions.Center, local: true);
            var twoCheck = PicLocal(twoCard.transform, "Check", "controls/checkbox_checked", 360, -160, 70);
            Pic(safe, "Tape", "doodles/red_tape_clean_edges", 150, 1200, 90, 0);

            var diffLabel = Txt(safe, "DifficultyLabel", "Độ khó <size=80%>(khi chơi với máy)</size>", 120, 1290, 880, 70, 46, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var diff = Options(safe, "Difficulty", new[] { "Dễ", "Vừa", "Khó" }, 1385, 270, 120);

            var resume = Btn(safe, "ResumeButton", "gray", 560, 1540, 860, 120, "Chơi tiếp ván dở", null, 0, 40);
            var start = Btn(safe, "StartButton", "yellow", 560, 1700, 860, 150, "Bắt đầu", "icons/play_clean_edges", 80);

            var mode = root.gameObject.AddComponent<ModeSelectScreen>();
            Set(mode, "backButton", back);
            Set(mode, "aiCard", aiCard);
            Set(mode, "twoPlayerCard", twoCard);
            Set(mode, "aiCardOn", Spr("panels/mode_card_yellow_selected"));
            Set(mode, "aiCardOff", Spr("panels/mode_card_yellow_normal"));
            Set(mode, "twoPlayerCardOn", Spr("panels/mode_card_blue_selected"));
            Set(mode, "twoPlayerCardOff", Spr("panels/mode_card_blue_normal"));
            Set(mode, "aiCheck", aiCheck.gameObject);
            Set(mode, "twoPlayerCheck", twoCheck.gameObject);
            Set(mode, "difficulty", diff);
            Set(mode, "difficultyLabel", diffLabel);
            Set(mode, "resumeButton", resume);
            Set(mode, "resumeLabel", Label(resume));
            Set(mode, "startButton", start);
            return mode;
        }

        private static GameScreen BuildGame(Transform canvas, BoardView board, SettingsScreen settings)
        {
            var root = MakeScreen(canvas, "GameScreen");
            var hud = Stretch(root, "Hud");
            var safe = Safe(hud);

            // Top block: header + whose turn. Its mint fill hides the board when it is zoomed under the bar.
            var top = Block(safe, "TopBlock", true, 400);
            var back = HeaderIn(top, "Chơi với máy - Dễ", out var title, withGear: true, out var gear);
            Pic(top, "Pencil", "icons/pencil_clean_edges", 130, 300, 80);
            var pill = Sliced(top, "TurnPill", "panels/blank_paper_panel", 500, 300, 600, 110, 1f);
            var status = Txt(pill, "Status", "Lượt của bạn (X)", 0, 0, 560, 90, 44, Ink, TextAlignmentOptions.Center, local: true);

            // Bottom block: fit / undo / new game, then the score panel
            var bottom = Block(safe, "BottomBlock", false, 640);
            var fit = Btn(bottom, "FitButton", "gray", 540, 1340, 400, 90, "Vừa màn hình", "icons/icon_fit_screen", 46, 34, fromBottom: true);
            var size = Txt(bottom, "BoardSize", "Bàn 15 × 15 · tối đa 20 × 20", 540, 1340, 900, 60, 34, Quiet, TextAlignmentOptions.Center, fromBottom: true);
            var undo = Btn(bottom, "UndoButton", "gray", 300, 1450, 420, 110, "Đi lại", null, 0, 46, fromBottom: true);
            var newGame = Btn(bottom, "NewGameButton", "yellow", 780, 1450, 420, 110, "Ván mới", "icons/replay", 50, 42, fromBottom: true);
            Txt(bottom, "ScoreTitle", "Tỉ số", 110, 1555, 300, 70, 46, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true, fromBottom: true);
            Sliced(bottom, "ScorePanel", "panels/score_panel", 540, 1740, 780, 280, 0, fromBottom: true);
            var leftName = Txt(bottom, "LeftName", "Bạn (X)", 330, 1690, 340, 60, 40, Ink, TextAlignmentOptions.Center, fromBottom: true);
            var leftScore = Txt(bottom, "LeftScore", "0", 330, 1780, 200, 90, 70, Red, TextAlignmentOptions.Center, fromBottom: true);
            Txt(bottom, "Dash", "–", 540, 1760, 80, 80, 64, Ink, TextAlignmentOptions.Center, fromBottom: true);
            var aiIcon = Pic(bottom, "Robot", "icons/robot_clean_edges", 880, 1690, 80, fromBottom: true);
            var rightName = Txt(bottom, "RightName", "Máy (O)", 740, 1690, 300, 60, 40, Ink, TextAlignmentOptions.Center, fromBottom: true);
            var rightScore = Txt(bottom, "RightScore", "0", 740, 1780, 200, 90, 70, Blue, TextAlignmentOptions.Center, fromBottom: true);
            Pic(bottom, "BlueStar", "doodles/blue_star_white_fill_transparent", 960, 1560, 70, fromBottom: true);

            var result = BuildResult(root);

            var gs = root.gameObject.AddComponent<GameScreen>();
            Set(gs, "board", board);
            Set(gs, "hud", hud.gameObject);
            Set(gs, "modeLabel", title);
            Set(gs, "menuButton", back);
            Set(gs, "settingsButton", gear);
            Set(gs, "settingsScreen", settings);
            Set(gs, "leftName", leftName);
            Set(gs, "rightName", rightName);
            Set(gs, "leftScore", leftScore);
            Set(gs, "rightScore", rightScore);
            Set(gs, "aiIcon", aiIcon.gameObject);
            Set(gs, "statusText", status);
            Set(gs, "boardSizeText", size);
            Set(gs, "undoButton", undo);
            Set(gs, "newGameButton", newGame);
            Set(gs, "fitButton", fit);
            Set(gs, "areaTop", top);
            Set(gs, "areaBottom", bottom);
            Set(gs, "resultScreen", result);
            return gs;
        }

        private static ResultScreen BuildResult(RectTransform gameRoot)
        {
            var root = Stretch(gameRoot, "ResultScreen");
            Background(root);
            var safe = Safe(root);

            Pic(safe, "Confetti", "results/confetti_burst_color", 560, 330, 900);
            Pic(safe, "RedStar", "decorations/stars/red_star", 890, 760, 90, 10);
            Pic(safe, "BlueStar", "decorations/stars/blue_star", 230, 820, 80, -8);
            Pic(safe, "Tape", "decorations/tape/red_tape_tall", 150, 1040, 80);

            var title = Txt(safe, "Title", "Bạn thắng!", 560, 380, 900, 180, 128, Blue, TextAlignmentOptions.Center);
            title.transform.localRotation = Quaternion.Euler(0, 0, 5);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 64;
            title.fontSizeMax = 128;
            var subtitle = Txt(safe, "Subtitle", "Nối 5 quân liền thắng!", 560, 530, 860, 70, 46, Ink, TextAlignmentOptions.Center);
            subtitle.transform.localRotation = Quaternion.Euler(0, 0, 5);
            var art = Pic(safe, "Art", "results/trophy_celebration", 560, 820, 480);
            art.rectTransform.sizeDelta = new Vector2(480, 420); // fixed box; lose/draw art keeps its own aspect inside

            Sliced(safe, "ScorePanel", "panels/score_panel", 560, 1160, 660, 230, 0);
            var leftName = Txt(safe, "LeftName", "Bạn (X)", 400, 1105, 300, 60, 40, Ink, TextAlignmentOptions.Center);
            var rightName = Txt(safe, "RightName", "Máy (O)", 720, 1105, 300, 60, 40, Ink, TextAlignmentOptions.Center);
            var leftScore = Txt(safe, "LeftScore", "0", 400, 1190, 200, 100, 80, Blue, TextAlignmentOptions.Center);
            Txt(safe, "Dash", "–", 560, 1180, 80, 90, 72, Ink, TextAlignmentOptions.Center);
            var rightScore = Txt(safe, "RightScore", "0", 720, 1190, 200, 100, 80, Red, TextAlignmentOptions.Center);

            var again = Btn(safe, "PlayAgainButton", "yellow", 560, 1390, 720, 140, "Chơi lại", "icons/replay", 80);
            var menu = Btn(safe, "MenuButton", "gray", 560, 1550, 720, 140, "Về menu", "icons/home", 90);
            var diff = Btn(safe, "ChangeDifficultyButton", "blue", 560, 1710, 720, 140, "Đổi độ khó", "icons/settings_gear_clean_edges", 85);
            var review = TextButton(safe, "ReviewButton", "<u>Xem lại bàn cờ</u>", 560, 1840, 500, 80, 40);

            var rs = root.gameObject.AddComponent<ResultScreen>();
            Set(rs, "titleText", title);
            Set(rs, "subtitleText", subtitle);
            Set(rs, "art", art);
            Set(rs, "winArt", Spr("results/trophy_celebration"));
            Set(rs, "loseArt", Spr("icons/robot"));
            Set(rs, "drawArt", Spr("icons/two_players_clean_edges"));
            Set(rs, "leftName", leftName);
            Set(rs, "rightName", rightName);
            Set(rs, "leftScore", leftScore);
            Set(rs, "rightScore", rightScore);
            Set(rs, "playAgainButton", again);
            Set(rs, "menuButton", menu);
            Set(rs, "changeDifficultyButton", diff);
            Set(rs, "reviewButton", review);
            root.gameObject.SetActive(false);
            return rs;
        }

        private static SettingsScreen BuildSettings(Transform canvas)
        {
            var root = MakeScreen(canvas, "SettingsScreen");
            Background(root);
            var safe = Safe(root);
            var back = Header(safe, "Cài đặt");

            // Sound card
            Pic(safe, "MusicIcon", "icons/music_note", 145, 290, 70);
            Txt(safe, "SoundTitle", "Âm thanh", 200, 290, 600, 80, 52, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            Sliced(safe, "SoundCard", "panels/notebook_note", 560, 530, 900, 360, 0);
            Pic(safe, "VolumeIcon", "icons/volume_icon_clean_edges", 200, 425, 70);
            var slider = MakeSlider(safe, 610, 425, 640);
            Txt(safe, "SoundLabel", "Bật âm thanh", 200, 535, 500, 70, 44, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var sound = Toggle(safe, "SoundToggle", 860, 535);
            Txt(safe, "VibrationLabel", "Rung", 200, 640, 500, 70, 44, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var vib = Toggle(safe, "VibrationToggle", 860, 640);

            // Who moves first against the AI
            Txt(safe, "FirstTitle", "Ai đi trước <size=80%>(chơi với máy)</size>", 120, 790, 880, 70, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var first = Options(safe, "FirstPlayer", new[] { "Bạn", "Máy" }, 885, 400, 110);
            var firstNote = Txt(safe, "FirstNote", "", 560, 965, 900, 50, 32, Quiet, TextAlignmentOptions.Center);

            // Difficulty
            Pic(safe, "ChartIcon", "icons/bar_chart", 145, 1060, 70);
            Txt(safe, "DifficultyTitle", "Độ khó <size=80%>(chơi với máy)</size>", 200, 1060, 820, 80, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var diff = Options(safe, "Difficulty", new[] { "Dễ", "Vừa", "Khó" }, 1160, 270, 110);

            // Theme: preview thumbnails above the option buttons
            Pic(safe, "PaletteIcon", "icons/palette", 145, 1290, 70);
            Txt(safe, "ThemeTitle", "Giao diện", 200, 1290, 600, 80, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var thumbs = new[] { "themes/theme_default_selected", "themes/theme_grid", "themes/theme_minimal_dark" };
            for (int i = 0; i < 3; i++)
                Pic(safe, "ThemePreview" + i, thumbs[i], 560 + (i - 1) * 300, 1450, 200);
            var theme = Options(safe, "Theme", new[] { "Mặc định", "Giấy ô li", "Tối giản" }, 1620, 270, 100, 38);
            Txt(safe, "ThemeNote", "Giấy ô li và Tối giản: chờ asset giao diện từ designer", 560, 1715, 900, 50, 30, Quiet, TextAlignmentOptions.Center);

            var ss = root.gameObject.AddComponent<SettingsScreen>();
            Set(ss, "backButton", back);
            Set(ss, "volumeSlider", slider);
            Set(ss, "soundToggle", sound);
            Set(ss, "vibrationToggle", vib);
            Set(ss, "toggleOn", Spr("controls/toggle_on"));
            Set(ss, "toggleOff", Spr("controls/toggle_off"));
            Set(ss, "firstPlayer", first);
            Set(ss, "firstPlayerNote", firstNote);
            Set(ss, "difficulty", diff);
            Set(ss, "theme", theme);
            return ss;
        }

        // ================= Building blocks =================

        private static RectTransform MakeScreen(Transform parent, string name) => Stretch(parent, name);

        private static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform Safe(RectTransform parent)
        {
            var rt = Stretch(parent, "SafeArea");
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        /// <summary>
        /// Full-screen notebook page behind a screen, built in layers so it stays sharp on any screen size:
        /// repeated grid paper, a 9-sliced page edge, the spiral binding repeated down the left edge and
        /// loose doodles placed relative to the page. Blocks clicks to what is below.
        /// </summary>
        private static void Background(RectTransform screen)
        {
            var rt = Stretch(screen, "Background");
            rt.offsetMin = new Vector2(18, 18);
            rt.offsetMax = new Vector2(-18, -18);

            // Grid paper: one tile holds 7 × 7 cells and is shown 504 units wide (72 units per cell)
            var paper = Stretch(rt, "Paper");
            paper.offsetMin = new Vector2(20, 20);
            paper.offsetMax = new Vector2(-20, -20);
            var paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.sprite = Spr("backgrounds/bg_paper_tile");
            paperImg.type = Image.Type.Tiled;
            paperImg.pixelsPerUnitMultiplier = paperImg.sprite.rect.width / 504f;
            paperImg.raycastTarget = true;

            // Page edge: transparent inside, its 80 px band shown about 27 units wide
            var edge = Stretch(rt, "PageEdge");
            var edgeImg = edge.gameObject.AddComponent<Image>();
            edgeImg.sprite = Spr("backgrounds/bg_page_frame");
            edgeImg.type = Image.Type.Sliced;
            edgeImg.pixelsPerUnitMultiplier = 3f;
            edgeImg.raycastTarget = false;

            // Spiral binding: one tile holds two rings, repeated from top to bottom over the left edge
            const float spiralWidth = 80f;
            var spiral = new GameObject("Spiral", typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)spiral.transform;
            srt.SetParent(rt, false);
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(spiralWidth, -60);
            srt.anchoredPosition = new Vector2(22, 0);
            var spiralImg = spiral.GetComponent<Image>();
            spiralImg.sprite = Spr("backgrounds/notebook_spiral_tile");
            spiralImg.type = Image.Type.Tiled;
            spiralImg.pixelsPerUnitMultiplier = spiralImg.sprite.rect.width / spiralWidth;
            spiralImg.raycastTarget = false;

            // Doodles, placed as fractions of the page so they keep their spots on any aspect ratio
            Deco(rt, "PaperPlane", "doodles/doodle_paper_plane", 0.80f, 0.09f, 190, 0, 1f);
            Deco(rt, "PaperPlaneFaint", "doodles/doodle_paper_plane", 0.22f, 0.46f, 120, -12, 0.35f);
            Deco(rt, "Star", "doodles/doodle_star_outline", 0.90f, 0.33f, 80, 8, 1f);
            Deco(rt, "Star2", "doodles/doodle_star_outline", 0.88f, 0.69f, 64, -12, 1f);
            Deco(rt, "Swoosh", "doodles/doodle_swoosh", 0.80f, 0.90f, 260, 0, 1f);
        }

        /// <summary>A background doodle anchored at a fraction of its parent (x from the left, y from the top).</summary>
        private static void Deco(RectTransform parent, string name, string path, float ax, float ay, float size, float angle, float alpha)
        {
            var sprite = Spr(path);
            float aspect = sprite.rect.width / sprite.rect.height;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(ax, 1 - ay);
            r.sizeDelta = aspect >= 1 ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
            r.anchoredPosition = Vector2.zero;
            r.localRotation = Quaternion.Euler(0, 0, angle);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1, 1, 1, alpha);
        }

        /// <summary>A full-width block glued to the top or bottom edge; its mint fill masks the zoomed board.</summary>
        private static RectTransform Block(RectTransform parent, string name, bool topEdge, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, topEdge ? 1 : 0);
            rt.anchorMax = new Vector2(1, topEdge ? 1 : 0);
            rt.pivot = new Vector2(0.5f, topEdge ? 1 : 0);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = Mint;
            img.raycastTarget = true;
            return rt;
        }

        /// <summary>Places a rect by its centre in mockup pixels (from the top, or from the bottom edge).</summary>
        private static RectTransform Place(GameObject go, Transform parent, float x, float y, float w, float h, bool fromBottom = false, bool local = false)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w, h);
            if (local)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(x, -y);
            }
            else if (fromBottom)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(x - W / 2, H - y);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(x - W / 2, -y);
            }
            return rt;
        }

        private static Sprite Spr(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");
            if (s == null) throw new System.Exception("Missing sprite " + path);
            return s;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, bool fromBottom = false, bool local = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Place(go, parent, x, y, w, h, fromBottom, local);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A trimmed picture scaled so its longer side is <paramref name="size"/> px.</summary>
        private static Image Pic(Transform parent, string name, string path, float x, float y, float size, float angle = 0, bool fromBottom = false)
        {
            var sprite = Spr(path);
            float aspect = sprite.rect.width / sprite.rect.height;
            float w = aspect >= 1 ? size : size * aspect, h = aspect >= 1 ? size / aspect : size;
            var img = Img(parent, name, sprite, x, y, w, h, fromBottom, parent.GetComponent<Button>() != null);
            if (angle != 0) img.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            return img;
        }

        private static Image PicLocal(Transform parent, string name, string path, float x, float y, float size)
        {
            var sprite = Spr(path);
            float aspect = sprite.rect.width / sprite.rect.height;
            float w = aspect >= 1 ? size : size * aspect, h = aspect >= 1 ? size / aspect : size;
            return Img(parent, name, sprite, x, y, w, h, local: true);
        }

        /// <summary>A 9-sliced panel; <paramref name="cornerScale"/> = on-screen size of the corners relative to source pixels × 2.</summary>
        private static RectTransform Sliced(Transform parent, string name, string path, float x, float y, float w, float h, float cornerScale, bool fromBottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = Place(go, parent, x, y, w, h, fromBottom, parent.GetComponent<Button>() != null);
            var img = go.GetComponent<Image>();
            img.sprite = Spr(path);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale > 0 ? cornerScale : FitScale(img.sprite, w, h);
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Slice scale that makes the whole sprite fit inside w × h, so its corners never overlap and only the middle stretches.</summary>
        private static float FitScale(Sprite s, float w, float h) => Mathf.Max(s.rect.width / w, s.rect.height / h);

        /// <summary>A card button from <paramref name="path"/>_normal / _pressed / _disabled sprites.</summary>
        private static Button SlicedButton(Transform parent, string name, string path, float x, float y, float w, float h)
        {
            var rt = Sliced(parent, name, path + "_normal", x, y, w, h, 0);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { pressedSprite = Spr(path + "_pressed"), disabledSprite = Spr(path + "_disabled") };
            return b;
        }

        private static TMP_Text Txt(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align, bool pivotLeft = false, bool pivotRight = false, bool fromBottom = false, bool local = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            // Left/right aligned text is placed by its left/right edge rather than its centre
            float cx = pivotLeft ? x + w / 2 : pivotRight ? x - w / 2 : x;
            Place(go, parent, cx, y, w, h, fromBottom, local);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A button made of the three state sprites of one colour (normal / pressed / disabled), with an optional icon.</summary>
        private static Button Btn(Transform parent, string name, string colour, float x, float y, float w, float h,
            string label, string icon, float iconSize, float fontSize = 54, bool fromBottom = false)
        {
            var rt = Card(parent, name, colour, x, y, w, h, true, fromBottom);
            var b = rt.GetComponent<Button>();
            if (icon != null)
            {
                PicLocal(rt, "Icon", icon, -w / 2 + 50 + iconSize / 2, 0, iconSize);
                float left = 60 + iconSize; // label fills the space right of the icon
                Txt(rt, "Label", label, left / 2, 0, w - left - 40, h, fontSize, Ink, TextAlignmentOptions.Center, local: true);
            }
            else
            {
                Txt(rt, "Label", label, 0, 0, w - 60, h, fontSize, Ink, TextAlignmentOptions.Center, local: true);
            }
            return b;
        }

        /// <summary>A 9-sliced card from a button sprite; interactable cards become buttons with sprite swaps.</summary>
        private static RectTransform Card(Transform parent, string name, string colour, float x, float y, float w, float h,
            bool interactable = true, bool fromBottom = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = Place(go, parent, x, y, w, h, fromBottom, parent.GetComponent<Button>() != null);
            var img = go.GetComponent<Image>();
            img.sprite = Spr($"buttons/button_{colour}_normal_fixed");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = FitScale(img.sprite, w, h);
            img.raycastTarget = interactable;
            if (interactable)
            {
                var b = go.AddComponent<Button>();
                b.targetGraphic = img;
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState
                {
                    pressedSprite = Spr($"buttons/button_{colour}_pressed"),
                    highlightedSprite = null,
                    selectedSprite = null,
                    disabledSprite = Spr($"buttons/button_{colour}_disabled"),
                };
            }
            return rt;
        }

        /// <summary>An on/off switch from the toggle art; the screen swaps its sprite.</summary>
        private static Button Toggle(Transform parent, string name, float x, float y)
        {
            var img = Pic(parent, name, "controls/toggle_on", x, y, 170);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            return b;
        }

        private static Button TextButton(Transform parent, string name, string label, float x, float y, float w, float h, float size)
        {
            var t = Txt(parent, name, label, x, y, w, h, size, Blue, TextAlignmentOptions.Center);
            t.raycastTarget = true;
            var b = t.gameObject.AddComponent<Button>();
            b.targetGraphic = t;
            b.transition = Selectable.Transition.None;
            return b;
        }

        private static TMP_Text Label(Button b) => b.GetComponentInChildren<TMP_Text>();

        /// <summary>Header row of a sub-screen: back button + title (+ optional settings button on the right).</summary>
        private static Button Header(Transform parent, string title) => HeaderIn(parent, title, out _, false, out _);

        private static Button HeaderIn(Transform parent, string title, out TMP_Text titleText, bool withGear, out Button gear)
        {
            var back = IconButton(parent, "BackButton", "icons/back_arrow", 130, 150, 100);
            titleText = Txt(parent, "Title", title, 210, 150, 680, 100, 58, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            gear = withGear ? IconButton(parent, "SettingsButton", "icons/settings_gear_clean_edges", 970, 150, 100, "gray") : null;
            return back;
        }

        /// <summary>A square frame button (yellow or gray) with an icon on top.</summary>
        private static Button IconButton(Transform parent, string name, string icon, float x, float y, float size, string frame = "yellow")
        {
            var img = Pic(parent, name, $"buttons_square/button_frame_{frame}_normal", x, y, size);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { pressedSprite = Spr($"buttons_square/button_frame_{frame}_pressed") };
            PicLocal(img.transform, "Icon", icon, 0, 0, size * 0.58f);
            return b;
        }

        /// <summary>A row of option buttons centred horizontally (selected = green, others = gray).</summary>
        private static OptionGroup Options(Transform parent, string name, string[] labels, float y, float w, float h, float fontSize = 46)
        {
            var row = new GameObject(name, typeof(RectTransform));
            Place(row, parent, 560, y, W, h);
            var buttons = new Button[labels.Length];
            float gap = 30, total = labels.Length * w + (labels.Length - 1) * gap;
            for (int i = 0; i < labels.Length; i++)
            {
                float cx = -total / 2 + w / 2 + i * (w + gap);
                var rt = Card(row.transform, labels[i], "gray", 0, 0, w, h);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(cx, 0);
                buttons[i] = rt.GetComponent<Button>();
                Txt(rt, "Label", labels[i], 0, 0, w - 20, h, fontSize, Ink, TextAlignmentOptions.Center, local: true);
            }
            var group = row.AddComponent<OptionGroup>();
            SetArray(group, "options", buttons);
            Set(group, "selectedSprite", Spr("buttons/button_green_normal_fixed"));
            Set(group, "normalSprite", Spr("buttons/button_gray_normal_fixed"));
            Set(group, "disabledSprite", Spr("buttons/button_gray_disabled"));
            return group;
        }

        /// <summary>Volume slider: track, coral fill and knob from the slider art.</summary>
        private static UnityEngine.UI.Slider MakeSlider(Transform parent, float x, float y, float w)
        {
            var go = new GameObject("VolumeSlider", typeof(RectTransform));
            Place(go, parent, x, y, w, 80);
            var bg = Sliced(go.transform, "Track", "controls/slider_track_empty", 0, 0, w, 40, 0);
            bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f);
            bg.sizeDelta = new Vector2(0, 40); bg.anchoredPosition = Vector2.zero;
            bg.GetComponent<Image>().pixelsPerUnitMultiplier = 64f / 40f;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var fa = (RectTransform)fillArea.transform;
            fa.SetParent(go.transform, false);
            fa.anchorMin = new Vector2(0, 0.5f); fa.anchorMax = new Vector2(1, 0.5f);
            fa.sizeDelta = new Vector2(-40, 40); fa.anchoredPosition = Vector2.zero;
            var fill = Sliced(fillArea.transform, "Fill", "controls/slider_track_filled", 0, 0, 10, 40, 0);
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1);
            fill.sizeDelta = new Vector2(40, 0); fill.anchoredPosition = Vector2.zero;
            fill.GetComponent<Image>().pixelsPerUnitMultiplier = 64f / 40f;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            var ha = (RectTransform)handleArea.transform;
            ha.SetParent(go.transform, false);
            ha.anchorMin = Vector2.zero; ha.anchorMax = Vector2.one;
            ha.sizeDelta = new Vector2(-70, 0); ha.anchoredPosition = Vector2.zero;
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handle = (RectTransform)handleGo.transform;
            handle.SetParent(ha, false);
            handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(70, 0);
            handle.anchoredPosition = Vector2.zero;
            var knob = handleGo.GetComponent<Image>();
            knob.sprite = Spr("controls/slider_knob");
            knob.preserveAspect = true;
            knob.raycastTarget = true;

            var s = go.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = knob;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1; s.value = 1;
            s.transition = Selectable.Transition.None;
            return s;
        }

        // ================= Serialized field wiring =================

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field) ?? throw new System.Exception($"{target.GetType().Name}.{field} not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field) ?? throw new System.Exception($"{target.GetType().Name}.{field} not found");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
