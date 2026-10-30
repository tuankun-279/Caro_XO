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
            ("controls/dropdown_", new Vector4(50, 50, 50, 50)),
            ("panels/notebook_note", new Vector4(100, 80, 60, 80)),
            ("panels/score_panel", new Vector4(80, 50, 50, 50)),
            ("panels/blank_paper_panel", new Vector4(80, 80, 80, 80)),
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
            BuildPageTurn(cam);

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

            var scores = BuildRecentScores(safe);

            Pic(safe, "YellowStar", "doodles/yellow_star", 955, 1500, 70, 10);
            Pic(safe, "Pencil", "doodles/pencil_large", 965, 1760, 110, 0);

            var menu = root.gameObject.AddComponent<MenuScreen>();
            Set(menu, "gameScreen", game);
            Set(menu, "board", board);
            Set(menu, "modeSelect", mode);
            Set(menu, "settings", settings);
            Set(menu, "vsAiButton", vsAi);
            Set(menu, "twoPlayerButton", twoP);
            Set(menu, "settingsButton", sett);
            Set(menu, "vsAiLabel", Dyn(Label(vsAi)));
            Set(menu, "twoPlayerLabel", Dyn(Label(twoP)));
            SetArray(menu, "recentScores", scores);
            return menu;
        }

        /// <summary>
        /// "Tỉ số gần đây" block on the menu, centred under the buttons. Hierarchy:
        /// RecentScores ─ PaperPanel, Tape, TitleText, RowList ─ Row_AiEasy, Row_AiMedium, Row_AiHard, Row_TwoPlayers,
        /// and every row holds a ModeText (left) and a ScoreText (right). RowList is a vertical layout, so a row
        /// can be added, removed or resized without moving the others by hand. Returns the four ScoreTexts.
        /// </summary>
        private static TMP_Text[] BuildRecentScores(RectTransform safe)
        {
            const float width = 620, height = 400;
            var block = Place(new GameObject("RecentScores", typeof(RectTransform)), safe, 560, 1640, width, height);

            var panel = Sliced(block, "PaperPanel", "panels/notebook_note", 0, 0, width, height, 0);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            PicLocal(block, "Tape", "decorations/tape/red_tape_small", -285, -185, 120);
            Txt(block, "TitleText", "Tỉ số gần đây", 25, -140, 460, 70, 46, Ink, TextAlignmentOptions.Center, local: true);

            // Rows fill the paper below the title; the left inset clears the spiral rings of the panel art
            var list = new GameObject("RowList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var listRt = (RectTransform)list.transform;
            listRt.SetParent(block, false);
            listRt.anchorMin = Vector2.zero;
            listRt.anchorMax = Vector2.one;
            listRt.offsetMin = new Vector2(110, 35);   // left, bottom
            listRt.offsetMax = new Vector2(-55, -103); // right, top (below the title)
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            var rows = new (string name, string mode)[]
            {
                ("Row_AiEasy", "Máy – Dễ"),
                ("Row_AiMedium", "Máy – Vừa"),
                ("Row_AiHard", "Máy – Khó"),
                ("Row_TwoPlayers", "2 người"),
            };
            var scores = new TMP_Text[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new GameObject(rows[i].name, typeof(RectTransform));
                row.transform.SetParent(listRt, false);
                RowText(row.transform, "ModeText", rows[i].mode, 0f, 0.65f, TextAlignmentOptions.MidlineLeft);
                scores[i] = RowText(row.transform, "ScoreText", "0 - 0", 0.65f, 1f, TextAlignmentOptions.MidlineRight);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRt); // so the saved scene already has the row sizes
            return scores;
        }

        /// <summary>A text filling a horizontal slice (fractions of the row width) of a score row.</summary>
        private static TMP_Text RowText(Transform row, string name, string text, float fromX, float toX, TextAlignmentOptions align)
        {
            var t = Txt(row, name, text, 0, 0, 10, 10, 38, Ink, align, local: true);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(fromX, 0);
            rt.anchorMax = new Vector2(toX, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
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
            Set(mode, "resumeLabel", Dyn(Label(resume)));
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
            // Turn bar: robot (or player X) on the left, the player (or player O) on the right, label centred between.
            // TurnAvatars lights up the side whose turn it is and fades the other.
            var turnBar = Place(new GameObject("TurnBar", typeof(RectTransform)), top, 550, 300, 900, 130);
            var pill = Sliced(turnBar, "TurnPill", "panels/blank_paper_panel", 0, 0, 560, 110, 0);
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.anchoredPosition = Vector2.zero;
            var status = Txt(pill, "StatusText", "Lượt của bạn (X)", 0, 0, 520, 90, 44, Ink, TextAlignmentOptions.Center, local: true);
            var leftAvatar = PicLocal(turnBar, "LeftAvatar", "icons/robot_clean_edges", -370, 0, 110);
            var rightAvatar = PicLocal(turnBar, "RightAvatar", "icons/person_blue", 370, 0, 110);
            var avatars = turnBar.gameObject.AddComponent<TurnAvatars>();
            Set(avatars, "left", leftAvatar);
            Set(avatars, "right", rightAvatar);
            Set(avatars, "animator", AvatarAnimator(turnBar.gameObject, leftAvatar.rectTransform.anchoredPosition, rightAvatar.rectTransform.anchoredPosition));

            // Bottom block: fit / undo / new game, then the score panel
            var bottom = Block(safe, "BottomBlock", false, 640);
            var fit = Btn(bottom, "FitButton", "gray", 540, 1340, 400, 90, "Vừa màn hình", "icons/icon_fit_screen", 46, 34, fromBottom: true);
            var size = Txt(bottom, "BoardSize", "Bàn 15 × 15 · tối đa 20 × 20", 540, 1340, 900, 60, 34, Quiet, TextAlignmentOptions.Center, fromBottom: true);
            var undo = Btn(bottom, "UndoButton", "gray", 300, 1450, 420, 110, "Đi lại", null, 0, 46, fromBottom: true);
            var newGame = Btn(bottom, "NewGameButton", "yellow", 780, 1450, 420, 110, "Ván mới", "icons/replay", 50, 42, fromBottom: true);
            // Score: title centred above the panel, an icon (player / robot) over each score instead of names
            Txt(bottom, "ScoreTitle", "Tỉ số", 540, 1555, 400, 70, 46, Ink, TextAlignmentOptions.Center, fromBottom: true);
            Sliced(bottom, "ScorePanel", "panels/score_panel", 540, 1740, 780, 280, 0, fromBottom: true);
            var leftIcon = Pic(bottom, "LeftIcon", "icons/person_blue", 340, 1683, 90, fromBottom: true);
            var leftScore = Txt(bottom, "LeftScore", "0", 340, 1785, 200, 90, 70, Red, TextAlignmentOptions.Center, fromBottom: true);
            Txt(bottom, "Dash", "–", 560, 1770, 80, 80, 64, Ink, TextAlignmentOptions.Center, fromBottom: true);
            var rightIcon = Pic(bottom, "RightIcon", "icons/robot_clean_edges", 780, 1683, 90, fromBottom: true);
            var rightScore = Txt(bottom, "RightScore", "0", 780, 1785, 200, 90, 70, Blue, TextAlignmentOptions.Center, fromBottom: true);
            Pic(bottom, "BlueStar", "doodles/blue_star_white_fill_transparent", 960, 1560, 70, fromBottom: true);

            var result = BuildResult(root);

            var gs = root.gameObject.AddComponent<GameScreen>();
            Set(gs, "board", board);
            Set(gs, "hud", hud.gameObject);
            Set(gs, "modeLabel", title);
            Set(gs, "menuButton", back);
            Set(gs, "settingsButton", gear);
            Set(gs, "settingsScreen", settings);
            Set(gs, "leftIcon", leftIcon);
            Set(gs, "rightIcon", rightIcon);
            Set(gs, "turnAvatars", avatars);
            Set(gs, "robotSprite", Spr("icons/robot_clean_edges"));
            Set(gs, "personXSprite", Spr("icons/person_coral"));
            Set(gs, "personOSprite", Spr("icons/person_blue"));
            Set(gs, "leftScore", leftScore);
            Set(gs, "rightScore", rightScore);
            // the old robot icon (aiIcon) is replaced by LeftIcon / RightIcon
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

            Pic(safe, "Confetti", "results/confetti_burst_color", 560, 215, 1000); // bursts sit above the title
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
            Set(rs, "titleText", Dyn(title));
            Set(rs, "subtitleText", Dyn(subtitle));
            Set(rs, "art", art);
            Set(rs, "winArt", Spr("results/trophy_celebration"));
            Set(rs, "loseArt", Spr("results/result_lose"));
            Set(rs, "drawArt", Spr("results/result_draw"));
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

        /// <summary>
        /// Settings popup over the current screen: a blurred snapshot of the screen fills the background (tap
        /// it to close), and a paper card holds the settings with a × button. The settings keep the full-screen
        /// mockup coordinates inside "Content", a 1080 × 1920 frame whose top sits 90 units above the card, so
        /// moving or resizing the card does not change the layout inside it.
        /// </summary>
        private static SettingsScreen BuildSettings(Transform canvas)
        {
            var root = MakeScreen(canvas, "SettingsScreen");
            var group = root.gameObject.AddComponent<CanvasGroup>();

            // Blurred backdrop (the texture is captured when the popup opens)
            var backdrop = Stretch(root, "BlurBackdrop");
            var blur = backdrop.gameObject.AddComponent<RawImage>();
            blur.color = new Color(0.8f, 0.8f, 0.8f, 1f); // slightly darker so the card stands out
            blur.raycastTarget = true;
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = blur;
            backdropButton.transition = Selectable.Transition.None;

            var safe = Safe(root);
            // The card is nearly as big as the screen so the settings keep their full size
            const float cardTop = 60, cardHeight = 1790;
            var popup = Place(new GameObject("Popup", typeof(RectTransform)), safe, 540, cardTop + cardHeight / 2, 1050, cardHeight);
            PaperCard(popup);

            var content = Place(new GameObject("Content", typeof(RectTransform)), popup, 520, 0, W, H);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0, 70);

            Txt(content, "TitleText", "Cài đặt", 560, 150, 600, 100, 58, Ink, TextAlignmentOptions.Center);
            var close = CloseButton(content, 965, 150, 96);

            // Language picker (row at the top; its list drops down over the rows below)
            var language = BuildLanguagePicker(content, 280);

            // Sound card
            const float d = 100; // everything below the language row moved down by this much
            Pic(content, "MusicIcon", "icons/music_note", 145, 290 + d, 70);
            Txt(content, "SoundTitle", "Âm thanh", 200, 290 + d, 600, 80, 52, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            Sliced(content, "SoundCard", "panels/notebook_note", 560, 530 + d, 900, 360, 0);
            Pic(content, "VolumeIcon", "icons/volume_icon_clean_edges", 200, 425 + d, 70);
            var slider = MakeSlider(content, 610, 425 + d, 640);
            Txt(content, "SoundLabel", "Bật âm thanh", 200, 535 + d, 500, 70, 44, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var sound = Toggle(content, "SoundToggle", 860, 535 + d);
            Txt(content, "VibrationLabel", "Rung", 200, 640 + d, 500, 70, 44, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var vib = Toggle(content, "VibrationToggle", 860, 640 + d);

            // Who moves first against the AI
            Txt(content, "FirstTitle", "Ai đi trước <size=80%>(chơi với máy)</size>", 120, 790 + d, 880, 70, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var first = Options(content, "FirstPlayer", new[] { "Bạn", "Máy" }, 885 + d, 400, 110);
            var firstNote = Dyn(Txt(content, "FirstNote", "", 560, 965 + d, 900, 50, 32, Quiet, TextAlignmentOptions.Center));

            // Difficulty
            Pic(content, "ChartIcon", "icons/bar_chart", 145, 1060 + d, 70);
            Txt(content, "DifficultyTitle", "Độ khó <size=80%>(chơi với máy)</size>", 200, 1060 + d, 820, 80, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var diff = Options(content, "Difficulty", new[] { "Dễ", "Vừa", "Khó" }, 1160 + d, 270, 110);

            // Theme: preview thumbnails above the option buttons (smaller than before to make room for the language row)
            Pic(content, "PaletteIcon", "icons/palette", 145, 1290 + d, 70);
            Txt(content, "ThemeTitle", "Giao diện", 200, 1290 + d, 600, 80, 48, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);
            var thumbs = new[] { "themes/theme_notebook", "themes/theme_grid", "themes/theme_minimal_dark" };
            for (int i = 0; i < 3; i++)
                Pic(content, "ThemePreview" + i, thumbs[i], 560 + (i - 1) * 300, 1530, 160);
            var theme = Options(content, "Theme", new[] { "Mặc định", "Giấy ô li", "Tối giản" }, 1690, 270, 90, 38);
            language.transform.SetAsLastSibling();

            // Opening motion: an Animator clip (fade and scale-in), kept invisible on the first frames while the
            // screen behind is captured for the blur
            var open = new AnimationClip { frameRate = 60 };
            CaroAnimTools.GroupAlpha(open, "", (0, 0), (0.04f, 0), (0.2f, 1));
            CaroAnimTools.Scale(open, "SafeArea/Popup", (0, 0.9f), (0.04f, 0.9f), (0.2f, 1.02f), (0.27f, 1));
            CaroAnimTools.Save(open, "PopupOpen");
            root.gameObject.AddComponent<Animator>().runtimeAnimatorController = CaroAnimTools.SingleClip("PopupOpen", open);

            var ss = root.gameObject.AddComponent<SettingsScreen>();
            Set(ss, "backButton", close);
            Set(ss, "blurBackdrop", blur);
            Set(ss, "backdropButton", backdropButton);
            Set(ss, "popup", popup);
            Set(ss, "group", group);
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

        /// <summary>
        /// "Ngôn ngữ" row: globe icon, title and a paper field with the current language and a chevron. Tapping
        /// the field drops down a list with one row per language; a full-screen invisible blocker under the list
        /// closes it. Uses the language art of Round 8 when present and existing art otherwise.
        /// </summary>
        private static LanguageDropdown BuildLanguagePicker(RectTransform content, float y)
        {
            var root = Stretch(content, "LanguagePicker");
            if (SprOpt("icons/globe") != null) Pic(root, "GlobeIcon", "icons/globe", 145, y, 70);
            Txt(root, "LanguageTitle", "Ngôn ngữ", 200, y, 400, 80, 52, Ink, TextAlignmentOptions.MidlineLeft, pivotLeft: true);

            // Blocker first so the field and the list draw (and take taps) above it
            var blockerRt = Stretch(root, "Blocker");
            blockerRt.offsetMin = new Vector2(-3000, -3000);
            blockerRt.offsetMax = new Vector2(3000, 3000);
            var blockerImg = blockerRt.gameObject.AddComponent<Image>();
            blockerImg.color = new Color(0, 0, 0, 0);
            var blocker = blockerRt.gameObject.AddComponent<Button>();
            blocker.targetGraphic = blockerImg;
            blocker.transition = Selectable.Transition.None;

            const float fx = 770, fw = 380, fh = 100;
            Button field;
            if (SprOpt("controls/dropdown_field") != null)
            {
                var rt = Sliced(root, "Field", "controls/dropdown_field", fx, y, fw, fh, 0);
                var img = rt.GetComponent<Image>();
                img.raycastTarget = true;
                field = rt.gameObject.AddComponent<Button>();
                field.targetGraphic = img;
                field.transition = Selectable.Transition.ColorTint;
            }
            else field = Card(root, "Field", "gray", fx, y, fw, fh).GetComponent<Button>();
            var fieldLabel = Dyn(Txt(field.transform, "Label", "English", -35, 0, fw - 120, fh, 44, Ink, TextAlignmentOptions.Center, local: true));

            Image chevron;
            Sprite down = SprOpt("controls/chevron_down"), up = SprOpt("controls/chevron_up");
            if (down != null) chevron = PicLocal(field.transform, "Chevron", "controls/chevron_down", fw / 2 - 55, 0, 44);
            else
            {
                // Fallback: the back arrow turned to point down
                chevron = PicLocal(field.transform, "Chevron", "icons/back_arrow", fw / 2 - 55, 0, 44);
                chevron.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                down = chevron.sprite;
            }

            // Dropped-down list under the field
            const float rowH = 100;
            int count = System.Enum.GetValues(typeof(Language)).Length;
            float listH = count * rowH + 40, listTop = y + fh / 2 + 10;
            string listArt = SprOpt("controls/dropdown_list") != null ? "controls/dropdown_list" : "panels/blank_paper_panel";
            var list = Sliced(root, "List", listArt, fx, listTop + listH / 2, fw, listH, 0);
            list.GetComponent<Image>().raycastTarget = true; // taps on the list margin must not reach the blocker
            string checkArt = SprOpt("controls/check_mark") != null ? "controls/check_mark" : "controls/checkbox_checked";
            var rows = new Button[count];
            var labels = new TMP_Text[count];
            var checks = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var row = new GameObject(((Language)i).ToString(), typeof(RectTransform), typeof(Image));
                var rowRt = (RectTransform)row.transform;
                rowRt.SetParent(list, false);
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
                rowRt.sizeDelta = new Vector2(fw - 40, rowH);
                rowRt.anchoredPosition = new Vector2(0, -20 - rowH / 2 - i * rowH);
                var rowImg = row.GetComponent<Image>();
                rowImg.color = new Color(1, 1, 1, 0);
                rows[i] = row.AddComponent<Button>();
                rows[i].targetGraphic = rowImg;
                rows[i].transition = Selectable.Transition.None;
                labels[i] = Dyn(Txt(rowRt, "Label", Loc.NativeName((Language)i), -30, 0, fw - 120, rowH, 42, Ink, TextAlignmentOptions.MidlineLeft, local: true));
                checks[i] = PicLocal(rowRt, "Check", checkArt, (fw - 40) / 2 - 40, 0, 48).gameObject;
            }
            list.gameObject.SetActive(false);
            blockerRt.gameObject.SetActive(false);

            var picker = root.gameObject.AddComponent<LanguageDropdown>();
            Set(picker, "field", field);
            Set(picker, "fieldLabel", fieldLabel);
            Set(picker, "chevron", chevron);
            Set(picker, "chevronDown", down);
            Set(picker, "chevronUp", up);
            Set(picker, "list", list.gameObject);
            Set(picker, "blocker", blocker);
            SetArray(picker, "rows", rows);
            SetArray(picker, "rowLabels", labels);
            SetArray(picker, "rowChecks", checks);
            return picker;
        }

        /// <summary>
        /// Animator of the turn bar: TurnNone / TurnLeft / TurnRight clips (state = TurnAvatars.Side). The side
        /// to move is larger, opaque and bobs up and down; the other is smaller and faded.
        /// </summary>
        private static Animator AvatarAnimator(GameObject bar, Vector2 leftPos, Vector2 rightPos)
        {
            const float active = 1.15f, idle = 0.85f, faded = 0.45f, bob = 5f, period = 1.6f;
            AnimationClip Make(string name, bool leftOn, bool rightOn)
            {
                var clip = new AnimationClip { frameRate = 60 };
                void Side(string path, Vector2 pos, bool on)
                {
                    float sc = on ? active : idle;
                    CaroAnimTools.Scale(clip, path, (0, sc), (period, sc));
                    clip.SetCurve(path, typeof(Image), "m_Color.a", CaroAnimTools.Curve((0, on ? 1 : faded), (period, on ? 1 : faded)));
                    CaroAnimTools.Pos(clip, path, 'x', (0, pos.x), (period, pos.x));
                    if (on) CaroAnimTools.Pos(clip, path, 'y', (0, pos.y), (period / 4, pos.y + bob), (period / 2, pos.y), (period * 3 / 4, pos.y - bob), (period, pos.y));
                    else CaroAnimTools.Pos(clip, path, 'y', (0, pos.y), (period, pos.y));
                }
                Side("LeftAvatar", leftPos, leftOn);
                Side("RightAvatar", rightPos, rightOn);
                return CaroAnimTools.SaveLooping(clip, name);
            }
            var none = Make("TurnNone", false, false);
            var left = Make("TurnLeft", true, false);
            var right = Make("TurnRight", false, true);
            var animator = bar.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.keepAnimatorStateOnDisable = true;
            animator.runtimeAnimatorController = CaroAnimTools.IntStates("TurnAvatars", "Side", 0.15f, none, left, right);
            return animator;
        }

        /// <summary>
        /// Transition canvas drawn over the UI: a page holding a snapshot of the screen being left. The PageTurn
        /// clip folds it over the spiral on the left (rotation around its left edge, away from the camera), darkens
        /// it as it turns and shades the new page under it; PageTurn.cs only takes the snapshot and sets the trigger.
        /// </summary>
        private static void BuildPageTurn(Camera cam)
        {
            var old = GameObject.Find("Transition Canvas");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Transition Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 4.5f;
            canvas.sortingOrder = 40; // over the UI (10), under the splash (50)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var rt = (RectTransform)root.transform;

            // Invisible blocker: takes the taps while the page turns (the group decides when)
            var blocker = Stretch(rt, "Blocker").gameObject.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0);

            var under = Stretch(rt, "UnderShadow").gameObject.AddComponent<Image>();
            under.color = new Color(0.12f, 0.1f, 0.08f, 0f);
            under.raycastTarget = false;

            var page = Stretch(rt, "Page");
            page.pivot = new Vector2(0f, 0.5f); // turns around the spiral on the left edge
            page.offsetMin = page.offsetMax = Vector2.zero;
            var snap = Stretch(page, "Snapshot").gameObject.AddComponent<RawImage>();
            snap.raycastTarget = false;
            var shade = Stretch(page, "Shade").gameObject.AddComponent<Image>();
            shade.color = new Color(0.12f, 0.1f, 0.08f, 0f);
            shade.raycastTarget = false;
            page.gameObject.SetActive(false);

            const float turn = 0.5f;
            var idle = new AnimationClip { frameRate = 60 };
            CaroAnimTools.Active(idle, "Page", (0, 0), (1f / 60, 0));
            CaroAnimTools.ImageAlpha(idle, "UnderShadow", (0, 0), (1f / 60, 0));
            CaroAnimTools.Save(idle, "PageIdle");

            var clip = new AnimationClip { frameRate = 60 };
            CaroAnimTools.Active(clip, "Page", (0, 1), (turn, 0));
            // Slow lift, then the page falls over towards the spiral
            CaroAnimTools.Rot(clip, "Page", 'y', (0, 0), (0.12f, -12), (turn - 0.02f, -90), (turn, -90));
            CaroAnimTools.ScaleY(clip, "Page", (0, 1), (0.2f, 1.03f), (turn, 1));
            CaroAnimTools.ImageAlpha(clip, "Page/Shade", (0, 0), (0.15f, 0.08f), (turn, 0.45f));
            CaroAnimTools.ImageAlpha(clip, "UnderShadow", (0, 0.3f), (turn, 0));
            CaroAnimTools.Events(clip, new AnimationEvent { time = turn, functionName = "TurnFinished" });
            CaroAnimTools.Save(clip, "PageTurn");

            var animator = root.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.runtimeAnimatorController = CaroAnimTools.IdleAndTrigger("PageTurn", idle, clip, "Turn");

            var pt = root.AddComponent<PageTurn>();
            Set(pt, "animator", animator);
            Set(pt, "page", snap);
            Set(pt, "group", group);
        }

        /// <summary>A card of grid paper with the hand-drawn page edge (used for popups); it blocks clicks.</summary>
        private static void PaperCard(RectTransform card)
        {
            var paper = Stretch(card, "Paper");
            paper.offsetMin = new Vector2(20, 20);
            paper.offsetMax = new Vector2(-20, -20);
            var paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.sprite = Spr("backgrounds/bg_paper_tile");
            paperImg.type = Image.Type.Tiled;
            paperImg.pixelsPerUnitMultiplier = paperImg.sprite.rect.width / 504f;
            paperImg.raycastTarget = true;

            var edge = Stretch(card, "PageEdge");
            var edgeImg = edge.gameObject.AddComponent<Image>();
            edgeImg.sprite = Spr("backgrounds/bg_page_frame");
            edgeImg.type = Image.Type.Sliced;
            edgeImg.pixelsPerUnitMultiplier = 3f;
            edgeImg.raycastTarget = false;

            // Pins holding the card on the cork board
            Deco(card, "PinTopLeft", "doodles/pin_yellow", 0.05f, 0.004f, 64, -10, 1f);
            Deco(card, "PinBottomRight", "doodles/pin_red", 0.95f, 0.996f, 64, 10, 1f);
        }

        /// <summary>Red close button (white ×) for popups, with its pressed state.</summary>
        private static Button CloseButton(Transform parent, float x, float y, float size)
        {
            var img = Pic(parent, "CloseButton", "buttons_square/close_normal", x, y, size);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { pressedSprite = Spr("buttons_square/close_pressed") };
            return b;
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
            // Cork board behind the page, repeated so it stays sharp. The page fills the screen; the cork only
            // shows on the far left, between the spiral loops
            var cork = Stretch(screen, "Corkboard");
            var corkImg = cork.gameObject.AddComponent<Image>();
            corkImg.sprite = Spr("backgrounds/cork_tile");
            corkImg.type = Image.Type.Tiled;
            corkImg.pixelsPerUnitMultiplier = corkImg.sprite.rect.width / 420f; // one tile = 420 units
            corkImg.raycastTarget = true;

            var rt = Stretch(screen, "Background");

            // The spiral column starts at the left edge of the screen; the page starts where its loops wrap
            // around the page edge (47% across a loop)
            const float spiralWidth = 90f;
            const float edgeInTile = 240f / 508f;
            const float pageLeft = spiralWidth * edgeInTile;

            // Grid paper: one tile holds 7 × 7 cells and is shown 504 units wide (72 units per cell)
            var paper = Stretch(rt, "Paper");
            paper.offsetMin = new Vector2(pageLeft, 0);
            paper.offsetMax = Vector2.zero;
            var paperImg = paper.gameObject.AddComponent<Image>();
            paperImg.sprite = Spr("backgrounds/bg_paper_tile");
            paperImg.type = Image.Type.Tiled;
            paperImg.pixelsPerUnitMultiplier = paperImg.sprite.rect.width / 504f;
            paperImg.raycastTarget = true;

            // Page edge: transparent inside, its 80 px band shown about 27 units wide. Only its left side is
            // wanted, so the other three sides (and the rounded corners) lie outside the screen.
            var edge = Stretch(rt, "PageEdge");
            edge.offsetMin = new Vector2(pageLeft, -200);
            edge.offsetMax = new Vector2(200, 200);
            var edgeImg = edge.gameObject.AddComponent<Image>();
            edgeImg.sprite = Spr("backgrounds/bg_page_frame");
            edgeImg.type = Image.Type.Sliced;
            edgeImg.pixelsPerUnitMultiplier = 3f;
            edgeImg.raycastTarget = false;

            // Spiral binding: one tile is one wire loop and its punched hole. The page edge sits at 47% of the
            // tile width, so the loop wraps around it and the hole lands in the page margin. The column is kept
            // a whole number of loops tall and clear of the rounded page corners.
            var coil = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "backgrounds/notebook_spiral_coil.png");
            if (coil != null)
            {
                var spiral = new GameObject("Spiral", typeof(RectTransform), typeof(Image));
                var srt = (RectTransform)spiral.transform;
                srt.SetParent(rt, false);
                srt.anchorMin = new Vector2(0, 0.5f);
                srt.anchorMax = new Vector2(0, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = new Vector2(spiralWidth, 400);
                srt.anchoredPosition = new Vector2(spiralWidth / 2, 0); // flush with the left edge of the screen
                var spiralImg = spiral.GetComponent<Image>();
                spiralImg.sprite = coil;
                spiralImg.type = Image.Type.Tiled;
                spiralImg.pixelsPerUnitMultiplier = coil.rect.width / spiralWidth;
                spiralImg.raycastTarget = false;
                float tile = coil.rect.height * spiralWidth / coil.rect.width;
                spiral.AddComponent<WholeTileColumn>().Configure(tile, 12f);
            }

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

        /// <summary>A full-width block glued to the top or bottom edge; its grid-paper fill masks the zoomed board.</summary>
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
            img.sprite = Spr("backgrounds/bg_paper_tile");
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = img.sprite.rect.width / 504f; // same 72-unit grid as the other screens
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

        /// <summary>The sprite at <paramref name="path"/>, or null when that art has not been delivered.</summary>
        private static Sprite SprOpt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");

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
            // Fixed labels follow the chosen language; texts that a screen rewrites at run time opt out with Dyn()
            if (Loc.Has(text)) go.AddComponent<LocalizedText>().Key = text;
            return t;
        }

        /// <summary>Marks a text as written by its screen at run time, so it gets no LocalizedText.</summary>
        private static TMP_Text Dyn(TMP_Text t)
        {
            var l = t.GetComponent<LocalizedText>();
            if (l != null) Object.DestroyImmediate(l);
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
