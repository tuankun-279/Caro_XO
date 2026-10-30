using System.Collections.Generic;
using System.Linq;
using Caro.Game;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.EditorTools
{
    /// <summary>
    /// Builds the launch splash in the Game scene: its own canvas drawn over the UI canvas, holding the cork
    /// board, a grid-paper card with a red pin, the logo, the X and O pieces and the waving robot. The motion
    /// is one Animator clip (SplashIntro) made here from keyframes; its animation events call
    /// SplashScreen.Burst(i) to play the particle systems (cork crumbs, sparks, confetti).
    /// Uses the splash art of Round 8 (Art/UIv2/splash) when it is there and existing art otherwise, so the
    /// splash can be built before that art arrives and rebuilt once it does. Re-running replaces the old splash.
    /// </summary>
    public static class CaroSplashBuilder
    {
        private const string Art = "Assets/_Caro/Art/UIv2/";
        private const string OutDir = "Assets/_Caro/Art/Splash";
        private const float W = 1080f, H = 1920f;
        private const float Length = 2.1f;   // the intro
        private const float FadeEnd = 2.45f; // the fade-out after it, also the clip length
        private const int SortingOrder = 50; // above the UI canvas (10)

        private static readonly Color Ink = new Color(0.16f, 0.17f, 0.22f);
        private static readonly Color BluePiece = new Color(0.29f, 0.56f, 0.94f);
        private static readonly Color CoralPiece = new Color(0.94f, 0.31f, 0.35f);

        // Animated parts: path under the canvas → rest position (canvas units, centre origin)
        private static readonly Dictionary<string, Vector2> Rest = new Dictionary<string, Vector2>();

        [MenuItem("Caro/Build Splash")]
        public static void Build()
        {
            Rest.Clear();
            var cam = Camera.main;
            if (cam == null) throw new System.Exception("Main Camera not found");
            var old = GameObject.Find("Splash Canvas");
            if (old != null) Object.DestroyImmediate(old);
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Caro/Art", "Splash");

            // Canvas
            var root = new GameObject("Splash Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 4;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.5f;
            var rt = (RectTransform)root.transform;

            // Cork board, tiled like the other screens
            var cork = Stretch(rt, "Cork");
            var corkImg = cork.gameObject.AddComponent<Image>();
            corkImg.sprite = Spr("backgrounds/cork_tile");
            corkImg.type = Image.Type.Tiled;
            corkImg.pixelsPerUnitMultiplier = corkImg.sprite.rect.width / 420f;

            // Tap anywhere to skip (transparent, under everything else)
            var skipRt = Stretch(rt, "SkipArea");
            var skipImg = skipRt.gameObject.AddComponent<Image>();
            skipImg.color = new Color(0, 0, 0, 0);
            var skip = skipRt.gameObject.AddComponent<Button>();
            skip.targetGraphic = skipImg;
            skip.transition = Selectable.Transition.None;

            // The notebook page: fills the screen like the other screens, with the spiral flush with the left
            // edge; the cork only shows between the loops once the page has landed
            var paper = Stretch(rt, "Paper");
            Remember(paper);
            const float spiralWidth = 90f, pageLeft = spiralWidth * 240f / 508f;
            var paperFill = Stretch(paper, "Fill");
            paperFill.offsetMin = new Vector2(pageLeft, 0);
            var fillImg = paperFill.gameObject.AddComponent<Image>();
            fillImg.sprite = Spr("backgrounds/bg_paper_tile");
            fillImg.type = Image.Type.Tiled;
            fillImg.pixelsPerUnitMultiplier = fillImg.sprite.rect.width / 504f;
            var edge = Stretch(paper, "Edge");
            edge.offsetMin = new Vector2(pageLeft, -200); // only the left side of the page edge is on screen
            edge.offsetMax = new Vector2(200, 200);
            var edgeImg = edge.gameObject.AddComponent<Image>();
            edgeImg.sprite = Spr("backgrounds/bg_page_frame");
            edgeImg.type = Image.Type.Sliced;
            edgeImg.pixelsPerUnitMultiplier = 3f;
            var coil = SprOpt("backgrounds/notebook_spiral_coil");
            if (coil != null)
            {
                var spiral = new GameObject("Spiral", typeof(RectTransform), typeof(Image));
                var srt = (RectTransform)spiral.transform;
                srt.SetParent(paper, false);
                srt.anchorMin = srt.anchorMax = new Vector2(0, 0.5f);
                srt.sizeDelta = new Vector2(spiralWidth, 400);
                srt.anchoredPosition = new Vector2(spiralWidth / 2, 0);
                var spiralImg = spiral.GetComponent<Image>();
                spiralImg.sprite = coil;
                spiralImg.type = Image.Type.Tiled;
                spiralImg.pixelsPerUnitMultiplier = coil.rect.width / spiralWidth;
                spiralImg.raycastTarget = false;
                spiral.AddComponent<WholeTileColumn>().Configure(coil.rect.height * spiralWidth / coil.rect.width, 12f);
            }

            // Pieces on the paper (local to the paper, so they fall with it until they fly in)
            bool ring = SprOpt("splash/spark_ring") != null;
            if (ring)
            {
                // Squashed to the wide logo so the rays frame it without reaching the X and O
                var r = Pic(paper, "SparkRing", "splash/spark_ring", 0, -400, 700);
                r.preserveAspect = false;
                r.rectTransform.sizeDelta = new Vector2(960, 560);
            }
            Pic(paper, "Logo", "logo/logo_caro_xo_illustrated", 0, -400, 900);
            Piece(paper, "X", "splash/x_piece", "X", BluePiece, -210, 130);
            Piece(paper, "O", "splash/o_piece", "O", CoralPiece, 210, 130);
            Pic(rt, "Pin", SprOpt("splash/pin") != null ? "splash/pin" : "doodles/pin_red", 540, 70, 120);

            // Robot peeking up from the bottom edge, waving
            bool arm = SprOpt("splash/robot_body") != null && SprOpt("splash/robot_arm") != null;
            var robot = arm ? Pic(rt, "Robot", "splash/robot_body", 540, 1660, 440)
                            : Pic(rt, "Robot", "icons/robot_clean_edges", 540, 1690, 300);
            if (arm)
            {
                var armImg = Pic(robot.rectTransform, "Arm", "splash/robot_arm", 0, 0, 275, local: true);
                armImg.rectTransform.pivot = new Vector2(0.15f, 0.2f); // shoulder end, so it waves around the joint
                armImg.rectTransform.anchoredPosition = new Vector2(137, -132); // the pivot sits on the right shoulder
            }

            // Particles (drawn over the splash canvas)
            // Particle sheet (Round 8): 4 x 4 cells — confetti 0-11, star 12, spark 13, cork crumb 14, dust puff 15
            var sheet = ParticleMaterial();
            var particles = new List<ParticleSystem>
            {
                Particles(rt, "CorkCrumbs", 540, 70, sheet, (14, 15), count: 14, speed: (250, 520),
                    life: (0.5f, 0.85f), size: (18, 34), radius: 20, fall: 1400),
                Particles(rt, "SparksX", 330, 1090, sheet, (12, 14), count: 10, speed: (380, 620),
                    life: (0.35f, 0.55f), size: (36, 60), radius: 70, fall: 0),
                Particles(rt, "SparksO", 750, 1090, sheet, (12, 14), count: 10, speed: (380, 620),
                    life: (0.35f, 0.55f), size: (36, 60), radius: 70, fall: 0),
                Particles(rt, "Confetti", 540, 560, sheet, (0, 12), count: 70, speed: (900, 1700),
                    life: (1.1f, 1.5f), size: (36, 64), radius: 120, fall: 2000),
            };

            // Motion
            var animator = root.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            var clip = MakeClip(ring, arm);
            var controllerPath = OutDir + "/SplashIntro.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            animator.runtimeAnimatorController = controller;

            var splash = root.AddComponent<SplashScreen>();
            var so = new SerializedObject(splash);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("fadeStart").floatValue = Length;
            so.FindProperty("clipLength").floatValue = FadeEnd;
            var arr = so.FindProperty("bursts");
            arr.arraySize = particles.Count;
            for (int i = 0; i < particles.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = particles[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Turn off the "Made with Unity" splash: this one replaces it
            PlayerSettings.SplashScreen.show = false;

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Caro] Splash built ({(arm ? "Round 8 art" : "placeholder art")})");
        }

        // ---------------- Animation ----------------

        private static AnimationClip MakeClip(bool ring, bool arm)
        {
            var path = OutDir + "/SplashIntro.anim";
            AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { name = "SplashIntro", frameRate = 60 };

            // Paper drops from above, overshoots, settles with a slight tilt
            var p = Rest["Paper"];
            Pos(clip, "Paper", 'y', (0, p.y + 2400), (0.35f, p.y - 30), (0.45f, p.y + 12), (0.55f, p.y));
            Rot(clip, "Paper", (0, -4), (0.35f, 1f), (0.55f, 0f)); // lands square, so no cork shows at the edges
            // Pin pops in once the paper lands
            Scale(clip, "Pin", (0, 0), (0.45f, 0), (0.55f, 1.3f), (0.65f, 1));
            // X then O fly in from the sides and stamp down
            Pos(clip, "Paper/X", 'x', (0, Rest["Paper/X"].x - 1100), (0.5f, Rest["Paper/X"].x - 1100), (0.72f, Rest["Paper/X"].x));
            Rot(clip, "Paper/X", (0, -40), (0.5f, -40), (0.72f, -8));
            Scale(clip, "Paper/X", (0, 1), (0.72f, 1.35f), (0.82f, 1));
            Pos(clip, "Paper/O", 'x', (0, Rest["Paper/O"].x + 1100), (0.62f, Rest["Paper/O"].x + 1100), (0.84f, Rest["Paper/O"].x));
            Rot(clip, "Paper/O", (0, 40), (0.62f, 40), (0.84f, 8));
            Scale(clip, "Paper/O", (0, 1), (0.84f, 1.35f), (0.94f, 1));
            // Logo pops
            Scale(clip, "Paper/Logo", (0, 0), (0.9f, 0), (1.05f, 1.15f), (1.2f, 0.95f), (1.3f, 1));
            if (ring)
            {
                Scale(clip, "Paper/SparkRing", (0, 0), (0.95f, 0), (1.15f, 1.1f), (1.5f, 0.95f), (1.9f, 1.05f), (Length, 1));
            }
            // Robot rises from the bottom edge and waves
            var r = Rest["Robot"];
            Pos(clip, "Robot", 'y', (0, r.y - 800), (1.1f, r.y - 800), (1.35f, r.y + 20), (1.45f, r.y));
            if (arm) Rot(clip, "Robot/Arm", (0, 0), (1.4f, 0), (1.55f, 30), (1.7f, -10), (1.85f, 30), (2.0f, 0));
            else Rot(clip, "Robot", (0, 0), (1.45f, 0), (1.6f, 8), (1.75f, -8), (1.9f, 6), (2.05f, 0));

            // Fade-out of the whole splash; taps go to the menu as soon as it starts
            CaroAnimTools.GroupAlpha(clip, "", (0, 1), (Length, 1), (FadeEnd, 0));
            CaroAnimTools.GroupBlocks(clip, "", (0, 1), (Length, 0), (FadeEnd, 0));

            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = 0.5f, functionName = "Burst", intParameter = 0 },
                new AnimationEvent { time = 0.72f, functionName = "Burst", intParameter = 1 },
                new AnimationEvent { time = 0.84f, functionName = "Burst", intParameter = 2 },
                new AnimationEvent { time = 1.0f, functionName = "Burst", intParameter = 3 },
                new AnimationEvent { time = FadeEnd, functionName = "Finished" },
            });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationCurve Curve((float t, float v)[] keys)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            return c;
        }

        private static void Pos(AnimationClip clip, string path, char axis, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(RectTransform), "m_AnchoredPosition." + axis, Curve(keys));

        private static void Scale(AnimationClip clip, string path, params (float, float)[] keys)
        {
            var c = Curve(keys);
            clip.SetCurve(path, typeof(RectTransform), "m_LocalScale.x", c);
            clip.SetCurve(path, typeof(RectTransform), "m_LocalScale.y", c);
        }

        private static void Rot(AnimationClip clip, string path, params (float, float)[] keys)
        {
            var zero = AnimationCurve.Constant(0, keys[keys.Length - 1].Item1, 0);
            clip.SetCurve(path, typeof(RectTransform), "localEulerAnglesRaw.x", zero);
            clip.SetCurve(path, typeof(RectTransform), "localEulerAnglesRaw.y", zero);
            clip.SetCurve(path, typeof(RectTransform), "localEulerAnglesRaw.z", Curve(keys));
        }

        // ---------------- Particles ----------------

        /// <summary>The material of the particle sheet: Art/Splash/particles.png drawn with the unlit sprite shader.</summary>
        private static Material ParticleMaterial()
        {
            const string texPath = OutDir + "/particles.png", matPath = OutDir + "/SplashParticles.mat";
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.mipmapEnabled || !ti.alphaIsTransparency
                || ti.textureCompression != TextureImporterCompression.Uncompressed))
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) throw new System.Exception("Missing " + texPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Caro/Art/SpriteUnlit.mat"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A one-shot burst in canvas units; each particle shows one random cell of the sheet in [cells.from, cells.to).</summary>
        private static ParticleSystem Particles(RectTransform canvas, string name, float x, float y, Material sheet,
            (int from, int to) cells, int count, (float, float) speed, (float, float) life, (float, float) size, float radius, float fall)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);
            go.transform.localPosition = new Vector3(x - W / 2, H / 2 - y, -50);
            var ps = go.AddComponent<ParticleSystem>();
            Configure(ps, sheet, cells, count, speed, life, size, radius, fall);
            return ps;
        }

        private static void Configure(ParticleSystem ps, Material sheetMat, (int from, int to) cells, int count,
            (float, float) speed, (float, float) life, (float, float) size, float radius, float fall)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            main.maxParticles = count * 2;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.2f;
            shape.rotation = Vector3.zero;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            if (fall > 0)
            {
                var force = ps.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.Local;
                force.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                force.y = new ParticleSystem.MinMaxCurve(-fall, -fall);
                force.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.dampen = 0.08f;
                limit.limit = 4000;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = gr;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1, 0.8f)));

            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            // In scripts the start frame is a fraction of the whole sheet (0-1), not a frame number
            const float tiles = 16f;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(cells.from / tiles, (cells.to - 0.01f) / tiles);
            sheet.cycleCount = 1;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = sheetMat;
            r.sortingOrder = SortingOrder + 1;
        }

        // ---------------- Layout helpers ----------------

        private static RectTransform Stretch(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>An empty centre-anchored group at mockup position (x, y) from the top-left.</summary>
        private static RectTransform Group(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x - W / 2, H / 2 - y);
            Remember(rt);
            return rt;
        }

        /// <summary>A picture whose longer side is <paramref name="size"/>; mockup position, or local to the parent centre.</summary>
        private static Image Pic(RectTransform parent, string name, string path, float x, float y, float size, bool local = false)
        {
            var sprite = Spr(path);
            float aspect = sprite.rect.width / sprite.rect.height;
            var rt = Group(parent, name, 0, 0, aspect >= 1 ? size : size * aspect, aspect >= 1 ? size / aspect : size);
            bool inGroup = local || parent.name != "Splash Canvas";
            rt.anchoredPosition = inGroup ? new Vector2(x, -y) : new Vector2(x - W / 2, H / 2 - y);
            Remember(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>An X or O piece: Round 8 art when delivered, otherwise the letter in the piece colour.</summary>
        private static void Piece(RectTransform parent, string name, string art, string letter, Color color, float x, float y)
        {
            if (SprOpt(art) != null) { Pic(parent, name, art, x, y, 250, local: true); return; }
            var rt = Group(parent, name, 0, 0, 260, 260);
            rt.anchoredPosition = new Vector2(x, -y);
            Remember(rt);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Caro/Art/Fonts/Pangolin SDF.asset");
            t.text = letter;
            t.fontSize = 260;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.outlineColor = Ink;
            t.outlineWidth = 0.2f;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
        }

        private static void Remember(RectTransform rt)
        {
            var path = AnimationUtility.CalculateTransformPath(rt, rt.root);
            Rest[path] = rt.anchoredPosition;
        }

        private static Sprite SprOpt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + path + ".png");

        private static Sprite Spr(string path)
        {
            var s = SprOpt(path);
            if (s == null) throw new System.Exception("Missing sprite " + path);
            return s;
        }
    }
}
