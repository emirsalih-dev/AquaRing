using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Aquaring.Gameplay;
using Aquaring.Input;
using Aquaring.UI;
using Aquaring.Managers;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Aquaring.EditorTools
{
    /// <summary>
    /// One-click assembler for the Aquaring v0 prototype scene. Menu:
    /// <c>Aquaring ▸ Build Prototype Scene</c>.
    ///
    /// It creates <c>Assets/Scenes/Aquaring.unity</c> from scratch, wires every
    /// component together and adds the scene to Build Settings as scene 0, so the
    /// only thing left to do is press Play.
    ///
    /// Layout (world units, XY plane, physics is 2D-only):
    ///   tank inner walls  x ∈ [-2.55, 2.55]   floor top y ≈ -3.45
    ///   peg               rises from the floor at x = 0
    ///   catch zone        (0, -2.95) – ring wins when it settles here, centred
    ///   ring spawn        (-1.6, -2.95) – resting on the floor, off to one side
    /// The 2.5D look is camera-side: a slightly tilted perspective camera plus
    /// shaded sprites and floor shadows.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Aquaring.unity";

        [MenuItem("Aquaring/Build Prototype Scene", priority = 0)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Aquaring", "Exit Play mode before building the scene.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material spriteMat = GetUnlitSpriteMaterial();

            BuildCamera();
            BuildTank(spriteMat);
            var input = BuildInputRouter();
            var ring = BuildRing(spriteMat, out Transform ringShadow);
            var peg = BuildPeg(spriteMat);
            var winPanel = BuildUI();
            BuildEventSystem();

            // hook the floor shadow to the ring
            ringShadow.GetComponent<GroundShadow>().SetTarget(ring.transform);

            // point the ring at its input source explicitly (it can also self-resolve)
            var ringSo = new SerializedObject(ring);
            ringSo.FindProperty("_jetInputSource").objectReferenceValue = input;
            ringSo.ApplyModifiedPropertiesWithoutUndo();

            // game manager ties it all together
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            SerializedObject so = new SerializedObject(gm);
            so.FindProperty("_ring").objectReferenceValue = ring;
            so.FindProperty("_peg").objectReferenceValue = peg;
            so.FindProperty("_input").objectReferenceValue = input;
            so.FindProperty("_winPanel").objectReferenceValue = winPanel;
            so.FindProperty("_spawnPosition").vector2Value = new Vector2(-1.6f, -2.95f);
            so.ApplyModifiedPropertiesWithoutUndo();

            ConfigurePhysics2D();

            // save + register
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=#39c>Aquaring</color>: prototype scene built at " + ScenePath +
                      ". Press Play – hold the bottom-left / bottom-right buttons (or A / D) to lift the ring onto the peg.");
            EditorUtility.DisplayDialog("Aquaring",
                "Prototype scene built and saved to:\n" + ScenePath +
                "\n\nIt is now scene 0 in Build Settings. Press Play to test.", "Nice");
        }

        // ------------------------------------------------------------- camera

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            // Raised and pitched down ~9° so the play field reads as a receding
            // plane (2.5D) while the physics stays flat on the XY plane at z = 0.
            go.transform.SetPositionAndRotation(new Vector3(0f, 2.8f, -16f), Quaternion.Euler(9f, 0f, 0f));

            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;           // perspective -> depth / 2.5D
            cam.fieldOfView = 44f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.06f, 0.12f, 1f);

            go.AddComponent<AudioListener>();

            // URP: make sure the camera has its additional data so it renders.
#if UNITY_2022_1_OR_NEWER
            try
            {
                var t = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (t != null && go.GetComponent(t) == null)
                    go.AddComponent(t);
            }
            catch { /* not a URP project – fine */ }
#endif
        }

        // ------------------------------------------------------------- tank

        private static void BuildTank(Material spriteMat)
        {
            var root = new GameObject("WaterTank");
            root.transform.position = new Vector3(0f, 0f, 0.2f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteFactory.Tank;
            sr.sharedMaterial = spriteMat;
            sr.sortingOrder = -20;
            bg.transform.localScale = new Vector3(5.4f, 9.0f, 1f);

            // solid boundary walls (2D). Multiple colliders on one object.
            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);
            walls.transform.localPosition = Vector3.zero;
            AddWall(walls, new Vector2(-2.55f, 0.3f), new Vector2(0.3f, 9f));   // left
            AddWall(walls, new Vector2(2.55f, 0.3f), new Vector2(0.3f, 9f));    // right
            AddWall(walls, new Vector2(0f, 4.2f), new Vector2(5.4f, 0.3f));     // ceiling
            AddWall(walls, new Vector2(0f, -3.6f), new Vector2(5.4f, 0.3f));    // floor (top surface y ≈ -3.45)
        }

        private static void AddWall(GameObject host, Vector2 center, Vector2 size)
        {
            var box = host.AddComponent<BoxCollider2D>();
            box.offset = center;
            box.size = size;
        }

        // ------------------------------------------------------------- input

        private static JetInputRouter BuildInputRouter()
        {
            var go = new GameObject("JetInputRouter");
            return go.AddComponent<JetInputRouter>();
        }

        // ------------------------------------------------------------- ring

        private static RingController BuildRing(Material spriteMat, out Transform shadow)
        {
            var go = new GameObject("Ring");
            go.transform.position = new Vector3(-1.6f, -2.95f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteFactory.Ring;
            sr.sharedMaterial = spriteMat;
            sr.sortingOrder = 10;
            go.transform.localScale = new Vector3(1.15f, 1.15f, 1f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 1f;
            body.mass = 1f;
            body.linearDamping = 1.0f;
            body.angularDamping = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.sharedMaterial = GetBouncyMaterial();

            go.AddComponent<RingController>();

            // floor shadow (separate object so it never rotates / rises with the ring)
            var shadowGo = new GameObject("RingShadow");
            var ssr = shadowGo.AddComponent<SpriteRenderer>();
            ssr.sprite = PlaceholderSpriteFactory.Shadow;
            ssr.sharedMaterial = spriteMat;
            ssr.color = new Color(0f, 0f, 0f, 0.34f);
            ssr.sortingOrder = -10;
            shadowGo.transform.localScale = new Vector3(1.4f, 0.5f, 1f);
            shadowGo.AddComponent<GroundShadow>();
            shadow = shadowGo.transform;

            return go.GetComponent<RingController>();
        }

        // ------------------------------------------------------------- peg

        private static PegTrigger BuildPeg(Material spriteMat)
        {
            var root = new GameObject("Peg");
            root.transform.position = new Vector3(0f, -0.5f, 0.1f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteFactory.Peg;
            sr.sharedMaterial = spriteMat;
            sr.sortingOrder = 0;
            visual.transform.localScale = new Vector3(1.1f, 4.0f, 1f);

            var pegShadow = new GameObject("PegShadow");
            pegShadow.transform.SetParent(root.transform, false);
            pegShadow.transform.localPosition = new Vector3(0f, -1.9f, 0f);
            var psr = pegShadow.AddComponent<SpriteRenderer>();
            psr.sprite = PlaceholderSpriteFactory.Shadow;
            psr.sharedMaterial = spriteMat;
            psr.color = new Color(0f, 0f, 0f, 0.28f);
            psr.sortingOrder = -11;
            pegShadow.transform.localScale = new Vector3(1.5f, 0.55f, 1f);

            // catch zone near the base of the peg
            var zone = new GameObject("CatchZone");
            zone.transform.SetParent(root.transform, false);
            zone.transform.localPosition = new Vector3(0f, -2.45f, 0f); // world y ≈ -2.95 (ring resting on the floor)
            var box = zone.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.7f, 0.95f);
            var trigger = zone.AddComponent<PegTrigger>();

            return trigger;
        }

        // ------------------------------------------------------------- UI

        private static WinPanel BuildUI()
        {
            var canvasGo = new GameObject("HUD Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            CreateJetButton(canvas.transform, JetSide.Left,
                new Vector2(0f, 0f), new Vector2(60f, 60f));
            CreateJetButton(canvas.transform, JetSide.Right,
                new Vector2(1f, 0f), new Vector2(-60f, 60f));

            CreateHint(canvas.transform);

            return CreateWinPanel(canvas.transform);
        }

        private static void CreateJetButton(Transform parent, JetSide side, Vector2 anchor, Vector2 pivotOffset)
        {
            var go = new GameObject($"JetButton_{side}");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380f, 380f);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pivotOffset;

            var img = go.AddComponent<Image>();
            img.sprite = PlaceholderSpriteFactory.Button;
            img.color = new Color(1f, 1f, 1f, 0.30f);
            img.raycastTarget = true;

            var btn = go.AddComponent<WaterJetButton>();
            var btnSo = new SerializedObject(btn);
            btnSo.FindProperty("_side").enumValueIndex = (int)side;
            btnSo.ApplyModifiedPropertiesWithoutUndo();

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lrt = label.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var text = label.AddComponent<Text>();
            text.text = side == JetSide.Left ? "LEFT\nJET" : "RIGHT\nJET";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 48;
            text.color = new Color(1f, 1f, 1f, 0.8f);
            text.font = BuiltinFont();
            text.raycastTarget = false;
        }

        private static void CreateHint(Transform parent)
        {
            var go = new GameObject("Hint");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta = new Vector2(900f, 120f);
            var text = go.AddComponent<Text>();
            text.text = "Hold the jets to float the ring onto the peg";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 40;
            text.color = new Color(1f, 1f, 1f, 0.55f);
            text.font = BuiltinFont();
        }

        private static WinPanel CreateWinPanel(Transform parent)
        {
            var panelGo = new GameObject("WinPanel");
            panelGo.transform.SetParent(parent, false);
            var prt = panelGo.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var dim = panelGo.AddComponent<Image>();
            dim.color = new Color(0f, 0.02f, 0.06f, 0.6f);

            // title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, 160f);
            trt.sizeDelta = new Vector2(900f, 200f);
            var title = titleGo.AddComponent<Text>();
            title.text = "Ring landed!";
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 84;
            title.color = Color.white;
            title.font = BuiltinFont();

            // retry button
            var btnGo = new GameObject("RetryButton");
            btnGo.transform.SetParent(panelGo.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, -80f);
            brt.sizeDelta = new Vector2(520f, 160f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.66f, 0.78f, 1f);
            var button = btnGo.AddComponent<Button>();

            var btnLabelGo = new GameObject("Text");
            btnLabelGo.transform.SetParent(btnGo.transform, false);
            var lrt = btnLabelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var btnLabel = btnLabelGo.AddComponent<Text>();
            btnLabel.text = "Try again";
            btnLabel.alignment = TextAnchor.MiddleCenter;
            btnLabel.fontSize = 56;
            btnLabel.color = Color.white;
            btnLabel.font = BuiltinFont();

            var winPanel = panelGo.AddComponent<WinPanel>();
            SerializedObject so = new SerializedObject(winPanel);
            so.FindProperty("_root").objectReferenceValue = panelGo;
            so.FindProperty("_retryButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            panelGo.SetActive(false); // GameManager shows it on win
            return winPanel;
        }

        // ------------------------------------------------------------- event system

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            // New Input System is the active handler in this project.
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // self-contained mouse / touch / navigation actions
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------- helpers

        private static void ConfigurePhysics2D()
        {
            Physics2D.gravity = new Vector2(0f, -9.81f);
        }

        private static Material _cachedSpriteMat;

        private static Material GetUnlitSpriteMaterial()
        {
            if (_cachedSpriteMat != null) return _cachedSpriteMat;

            const string path = "Assets/Materials/AquaringSprite.mat";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { _cachedSpriteMat = existing; return existing; }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "AquaringSprite" };
            AssetDatabase.CreateAsset(mat, path);
            _cachedSpriteMat = mat;
            return mat;
        }

        private static PhysicsMaterial2D _cachedBouncy;

        private static PhysicsMaterial2D GetBouncyMaterial()
        {
            if (_cachedBouncy != null) return _cachedBouncy;

            const string path = "Assets/Materials/RingBouncy.physicsMaterial2D";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null) { _cachedBouncy = existing; return existing; }

            var pm = new PhysicsMaterial2D("RingBouncy") { bounciness = 0.15f, friction = 0.35f };
            AssetDatabase.CreateAsset(pm, path);
            _cachedBouncy = pm;
            return pm;
        }

        private static Font BuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
