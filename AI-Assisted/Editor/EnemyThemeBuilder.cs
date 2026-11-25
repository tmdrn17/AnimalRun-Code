using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Animal_Run.Scripts.EditorTools
{
    // 테마마다 Synty 캐릭터로 적 프리팹을 만들고 MapTheme 에 연결한다.
    // - 적 동작(Idle / Run)은 이미 구워 둔 휴머노이드 클립(Humanoid Enemy Controller)을 쓴다.
    //   Synty 캐릭터는 모두 Humanoid 리그라 같은 클립이 리타깃되어 그대로 재생된다.
    // - 적 프리팹은 Synty 캐릭터를 루트로 두고, 기존 테마 적(TemplateEnemyPath)의 Enemy·피격 피드백·체력바를 옮겨 붙인다.
    // 메뉴: Tools/Animal Run/Build Themed Enemies
    // 외부 요청: Temp/ChunkThemeRequests/Enemies.request → Enemies.result, Enemies.png
    public static class EnemyThemeBuilder
    {
        private const string OutDir = "Assets/Animal Run/Prefabs/Enemies/";
        private const string ThemeDir = "Assets/Animal Run/Prefabs/Chunks/";
        private const string TemplateEnemyPath = OutDir + "Kingdom Village/Enemy Peasant_Male.prefab";
        private const string HumanoidDir = "Assets/Animal Run/Animations/Enemy/Humanoid/";
        private const string ControllerPath = "Assets/Animal Run/Animators/Enemy/Humanoid Enemy Controller.controller";

        private static readonly List<string> Report = new List<string>();

        // 테마 Id(청크 빌더와 같음) → 적 캐릭터
        private static readonly (string themeId, string[] characters)[] ThemeEnemies =
        {
            ("Kingdom Village", new[] { "FK:SM_Chr_Peasant_Male_01", "FK:SM_Chr_Peasant_Female_01", "FK:SM_Chr_Blacksmith_Male_01", "FK:SM_Chr_Merchant_01", "FK:SM_Chr_Bartender_01" }),
            ("Castle", new[] { "FK:SM_Chr_Soldier_Male_01", "FK:SM_Chr_Soldier_Female_01", "FK:SM_Chr_Headsman_01" }),
            ("Viking Snow", new[] { "VK:SM_Chr_Grunt_01_Brown", "VK:SM_Chr_Grunt_02_Black", "VK:SM_Chr_Warrior_01_White", "VK:SM_Chr_Shieldmadien_Brown" }),
            ("Magic Forest", new[] { "FK:SM_Chr_Hermit_01", "FK:SM_Chr_Mage_01", "FK:SM_Chr_FortuneTeller_01" }),
            ("Carnival", new[] { "HC:SM_Chr_Clown_Male_01", "HC:SM_Chr_Clown_Female_01", "HC:SM_Chr_Carny_01", "HC:SM_Chr_Strongman_01", "HC:SM_Chr_RingLeader_01", "HC:SM_Chr_Animatronic_01", "GN:SM_Gen_Chr_Skeleton_01" }),
            ("Elven Realm", new[] { "ER:SM_Chr_Soldier_Male_01", "ER:SM_Chr_Soldier_Female_01", "ER:SM_Chr_Knight_Male_01", "ER:SM_Chr_Nobility_Male_01" }),
            ("Enchanted Grove", new[] { "ER:SM_Chr_Rogue_Female_01", "ER:SM_Chr_Priestess_Female_01", "ER:SM_Chr_Scholar_Male_01" }),
            ("Misty Swamp", new[] { "GN:SM_Gen_Chr_Charred_01", "GN:SM_Gen_Chr_Skeleton_01", "GN:SM_Gen_Chr_Prisoner_Male_01", "GN:SM_Gen_Chr_Prisoner_Female_01" }),
        };

        private static readonly Dictionary<string, string> Packs = new Dictionary<string, string>
        {
            { "FK", "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters" },
            { "VK", "Assets/Synty/PolygonVikings/Prefabs/Characters" },
            { "GN", "Assets/Synty/PolygonGeneric/Prefabs/Characters" },
            { "HC", "Assets/Synty/PolygonHorrorCarnival/Prefabs/Characters" },
            { "ER", "Assets/Synty/PolygonElvenRealm/Prefabs/Characters" },
        };

        [MenuItem("Tools/Animal Run/Build Themed Enemies")]
        private static void BuildFromMenu()
        {
            foreach (string line in Build())
                Debug.Log("[EnemyThemeBuilder] " + line);
        }

        public static List<string> Build()
        {
            Report.Clear();

            AnimatorController controller = LoadHumanoidController();
            if (controller == null)
                return new List<string>(Report);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(TemplateEnemyPath) == null)
            {
                Log("FAIL 틀로 쓸 적 프리팹이 없음: " + TemplateEnemyPath);
                return new List<string>(Report);
            }

            foreach ((string themeId, string[] characters) in ThemeEnemies)
            {
                string dir = OutDir + themeId;
                EnsureFolder(dir);

                var enemies = new List<Enemy>();
                foreach (string character in characters)
                {
                    Enemy enemy = BuildEnemy(character, controller, dir);
                    if (enemy != null)
                        enemies.Add(enemy);
                }

                AssignToTheme(themeId, enemies);
            }

            AssetDatabase.SaveAssets();
            ReimportAndCheckControllers();
            return new List<string>(Report);
        }

        // 빌드 도중 저장한 프리팹은 파일에는 컨트롤러가 들어 있는데 메모리에서는 비어 보일 때가 있다(플레이하면 적이 T포즈로 미끄러짐).
        // 디스크 내용으로 다시 불러온 뒤 컨트롤러가 실제로 연결됐는지 확인한다.
        private static void ReimportAndCheckControllers()
        {
            int missing = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<Enemy>() == null)
                    continue;

                if (prefab.TryGetComponent(out Animator animator) == false || animator.runtimeAnimatorController == null)
                {
                    Log("FAIL 컨트롤러가 연결되지 않음: " + path);
                    missing++;
                }
            }

            if (missing == 0)
                Log("모든 적 프리팹에 휴머노이드 컨트롤러 연결 확인");
        }

        // ───────────────────────── 휴머노이드 컨트롤러 ─────────────────────────

        // 휴머노이드 Idle·Run 클립과 컨트롤러는 이미 구워 둔 것을 그대로 쓴다.
        // (mixamo 클립을 구울 때 쓰던 Farmer 뼈대는 프로젝트에서 지워서 다시 구울 수 없다.)
        private static AnimatorController LoadHumanoidController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Log("FAIL 휴머노이드 컨트롤러가 없음: " + ControllerPath);
                return null;
            }

            foreach (string stateName in new[] { "Idle", "Run" })
            {
                bool hasClip = false;
                foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name == stateName && child.state.motion != null)
                        hasClip = true;
                }

                if (hasClip == false)
                {
                    Log($"FAIL 휴머노이드 컨트롤러에 '{stateName}' 상태(클립)가 없음: {ControllerPath}");
                    return null;
                }
            }

            Log("휴머노이드 컨트롤러 → " + ControllerPath);
            return controller;
        }

        // 만든 적마다 휴머노이드 Idle·Run 클립을 직접 샘플링해 메시 맨 아래 높이와 좌우 폭을 잰다.
        // 발 높이가 0 근처면 지면에 닿아 있고, 폭이 T포즈(약 1.6m)보다 좁으면 동작이 실제로 입혀진 것이다.
        public static List<string> VerifyGround()
        {
            Report.Clear();
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidDir + "Humanoid Idle.anim");
            var run = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidDir + "Humanoid Run.anim");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<Enemy>() == null)
                    continue; // Enemy Group 처럼 적이 아닌 프리팹은 건너뛴다

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    string line = path.Replace(OutDir, "");
                    foreach (AnimationClip clip in new[] { idle, run })
                    {
                        MeasureClip(go, clip, 12, out float lowest, out float width);
                        line += $" / {clip.name.Replace("Humanoid ", "")} 발 {lowest:0.00}m 폭 {width:0.00}m";
                    }
                    Log("[Verify] " + line);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            return new List<string>(Report);
        }

        // 클립을 처음부터 끝까지 샘플링하며 메시의 가장 낮은 높이와 가장 넓은 좌우 폭을 잰다.
        private static void MeasureClip(GameObject go, AnimationClip clip, int samples, out float lowest, out float width)
        {
            lowest = float.MaxValue;
            width = 0f;
            var baked = new Mesh();
            try
            {
                SkinnedMeshRenderer[] skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < samples; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / samples);
                    float minX = float.MaxValue, maxX = float.MinValue;
                    foreach (SkinnedMeshRenderer skin in skins)
                    {
                        skin.BakeMesh(baked, true);
                        Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 p = toWorld.MultiplyPoint3x4(vertex);
                            lowest = Mathf.Min(lowest, p.y);
                            minX = Mathf.Min(minX, p.x);
                            maxX = Mathf.Max(maxX, p.x);
                        }
                    }
                    width = Mathf.Max(width, maxX - minX);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        // ───────────────────────── 적 프리팹 ─────────────────────────

        private static Enemy BuildEnemy(string character, AnimatorController controller, string dir)
        {
            GameObject characterPrefab = LoadCharacter(character);
            if (characterPrefab == null)
                return null;

            // 틀 적은 이 빌드 중에 다시 저장될 수 있어서 매번 새로 불러온다.
            var templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateEnemyPath);
            var template = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(template, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                string enemyName = "Enemy " + characterPrefab.name.Replace("SM_Chr_", "").Replace("SM_Gen_Chr_", "").Replace("_01", "");
                root.name = enemyName;
                root.layer = template.layer;
                root.tag = template.tag;

                // 적 판정은 거리로 하므로(Enemy · Bullet) 캐릭터에 딸린 콜라이더·리지드바디는 모두 뺀다.
                foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                    Object.DestroyImmediate(body);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                // 애니메이터: 캐릭터 아바타 + 휴머노이드 컨트롤러 (루트 모션은 쓰지 않고 Enemy 가 직접 움직인다)
                if (root.TryGetComponent(out Animator animator) == false)
                    animator = root.AddComponent<Animator>();
                if (animator.avatar == null || animator.avatar.isHuman == false)
                    animator.avatar = FindModelAvatar(root);
                if (animator.avatar == null || animator.avatar.isHuman == false)
                    Log($"WARN {enemyName}: 휴머노이드 아바타가 없어 애니메이션이 재생되지 않을 수 있음");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // 체력바: 캐릭터 머리 위로 옮긴다 (월드 크기 유지)
                Transform healthBarCanvas = FindChild(template.transform, "HealthBarCanvas");
                if (healthBarCanvas != null)
                {
                    healthBarCanvas.SetParent(root.transform, true);
                    float headY = RendererTop(root) + 0.35f;
                    Vector3 position = healthBarCanvas.position;
                    healthBarCanvas.position = new Vector3(position.x, headY, position.z);
                }
                else
                {
                    Log($"WARN {enemyName}: 틀 적에서 HealthBarCanvas 를 못 찾음");
                }

                // Enemy·피격 피드백: 틀 적의 값을 그대로 복사 (healthBar 참조는 옮긴 체력바를 그대로 가리킨다)
                var enemy = root.AddComponent<Enemy>();
                EditorUtility.CopySerialized(template.GetComponent<Enemy>(), enemy);
                if (template.TryGetComponent(out EnemyHitFeedback templateFeedback))
                    EditorUtility.CopySerialized(templateFeedback, root.AddComponent<EnemyHitFeedback>());

                var healthBarField = new SerializedObject(enemy).FindProperty("healthBar");
                if (healthBarField.objectReferenceValue == null)
                    Log($"WARN {enemyName}: healthBar 참조가 비었음");

                string path = $"{dir}/{enemyName}.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                Log($"{enemyName} ({character}) → {path} / 키 {RendererTop(root):0.00}m");
                return saved.GetComponent<Enemy>();
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
            }
        }

        private static void AssignToTheme(string themeId, List<Enemy> enemies)
        {
            string path = $"{ThemeDir}{themeId}/{themeId} Theme.asset";
            var mapTheme = AssetDatabase.LoadAssetAtPath<MapTheme>(path);
            if (mapTheme == null)
            {
                Log($"WARN 테마 에셋이 없어 적 연결을 건너뜀: {path} (Build Themed Chunks 를 먼저 실행하세요)");
                return;
            }

            var serialized = new SerializedObject(mapTheme);
            SerializedProperty list = serialized.FindProperty("enemyPrefabs");
            list.arraySize = enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            serialized.ApplyModifiedProperties();
            Log($"{themeId} 테마에 적 {enemies.Count}종 연결");
        }

        // 피격 플래시 확인용: 적마다 실제 EnemyHitFeedback 의 Awake·SetFlash(1) 를 불러 플래시 전(위)·후(아래)를 나란히 찍는다.
        public static List<string> CaptureFlashPreview(string pngPath)
        {
            Report.Clear();
            const int cell = 180;
            var prefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Animal Run/Prefabs/Enemies" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponent<EnemyHitFeedback>() != null)
                    prefabs.Add(prefab);
                if (prefabs.Count >= 8)
                    break;
            }

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var awake = typeof(EnemyHitFeedback).GetMethod("Awake", flags);
            var setFlash = typeof(EnemyHitFeedback).GetMethod("SetFlash", flags);
            var clearFlash = typeof(EnemyHitFeedback).GetMethod("ClearFlash", flags);

            var sheet = new Texture2D(cell * Mathf.Max(1, prefabs.Count), cell * 2, TextureFormat.RGB24, false);
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidDir + "Humanoid Idle.anim");
            try
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                    try
                    {
                        go.transform.SetPositionAndRotation(new Vector3(6000f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                        if (idle != null && go.GetComponent<Animator>()?.avatar != null)
                            idle.SampleAnimation(go, 0f);

                        var feedback = go.GetComponent<EnemyHitFeedback>();
                        awake.Invoke(feedback, null);

                        Texture2D before = Render(new Vector3(6000f, 1f, -4.2f), new Vector3(3f, 0f, 0f), 35f, cell, cell);
                        setFlash.Invoke(feedback, new object[] { 1f });
                        Texture2D after = Render(new Vector3(6000f, 1f, -4.2f), new Vector3(3f, 0f, 0f), 35f, cell, cell);
                        clearFlash.Invoke(feedback, null);

                        // 플래시 전후 평균 밝기 차이로 번쩍임이 실제로 보이는지 수치로도 남긴다.
                        float diff = AverageLuminance(after) - AverageLuminance(before);
                        Log($"[Flash] {prefabs[i].name}: 밝기 변화 {diff:+0.000;-0.000}");

                        sheet.SetPixels(i * cell, cell, cell, cell, before.GetPixels());
                        sheet.SetPixels(i * cell, 0, cell, cell, after.GetPixels());
                        Object.DestroyImmediate(before);
                        Object.DestroyImmediate(after);
                    }
                    finally
                    {
                        Object.DestroyImmediate(go);
                    }
                }

                sheet.Apply();
                File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
                Log("플래시 미리보기 → " + pngPath);
                return new List<string>(Report);
            }
            finally
            {
                Object.DestroyImmediate(sheet);
            }
        }

        private static float AverageLuminance(Texture2D tex)
        {
            float sum = 0f;
            Color[] pixels = tex.GetPixels();
            foreach (Color c in pixels)
                sum += c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return sum / pixels.Length;
        }

        // ───────────────────────── 미리보기 ─────────────────────────

        // 테마별 한 줄씩, 적이 Run 상태로 달리는 순간을 찍는다 (애니메이션이 실제로 입혀졌는지 확인용).
        public static List<string> CapturePreview(string pngPath)
        {
            Report.Clear();
            const int cell = 200, cols = 7;
            int rows = ThemeEnemies.Length;
            var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGB24, false);
            var holder = new GameObject("EnemyPreview");
            GameObject tempSun = null;
            try
            {
                if (Object.FindFirstObjectByType<Light>() == null)
                {
                    tempSun = new GameObject("PreviewSun");
                    var sun = tempSun.AddComponent<Light>();
                    sun.type = LightType.Directional;
                    tempSun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }

                var runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidDir + "Humanoid Run.anim");
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetParent(holder.transform);
                ground.transform.position = new Vector3(5000f, -0.05f, 0f);
                ground.transform.localScale = new Vector3(3f, 0.1f, 3f);

                for (int r = 0; r < rows; r++)
                {
                    string dir = OutDir + ThemeEnemies[r].themeId;
                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
                    for (int c = 0; c < cols; c++)
                    {
                        Texture2D tex;
                        if (c < guids.Length)
                        {
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[c]));
                            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                            go.transform.position = new Vector3(5000f, 0f, 0f);
                            go.transform.rotation = Quaternion.Euler(0f, 150f, 0f);

                            // 실제 게임과 같은 휴머노이드 Run 클립을 직접 입힌다. 지면(y=0) 판을 깔아 묻힘이 바로 보이게.
                            runClip.SampleAnimation(go, runClip.length * 0.25f);
                            tex = Render(new Vector3(5000f, 1.1f, -4.6f), new Vector3(6f, 0f, 0f), 35f, cell, cell);
                            Object.DestroyImmediate(go);
                        }
                        else
                        {
                            tex = new Texture2D(cell, cell, TextureFormat.RGB24, false);
                        }

                        sheet.SetPixels(c * cell, (rows - 1 - r) * cell, cell, cell, tex.GetPixels());
                        Object.DestroyImmediate(tex);
                    }
                }

                sheet.Apply();
                File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
                Log("미리보기 → " + pngPath);
                return new List<string>(Report);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(sheet);
                if (tempSun != null)
                    Object.DestroyImmediate(tempSun);
            }
        }

        private static Texture2D Render(Vector3 pos, Vector3 euler, float fov, int w, int h)
        {
            var go = new GameObject("PreviewCam");
            try
            {
                var cam = go.AddComponent<UnityEngine.Camera>();
                cam.transform.position = pos;
                cam.transform.eulerAngles = euler;
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                return tex;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ───────────────────────── 공통 ─────────────────────────

        private static GameObject LoadCharacter(string item)
        {
            string[] parts = item.Split(':');
            if (parts.Length == 2 && Packs.TryGetValue(parts[0], out string folder))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{parts[1]}.prefab");
                if (prefab != null)
                    return prefab;
            }

            Log("MISSING " + item);
            return null;
        }

        // 캐릭터 프리팹에 아바타가 없으면 메시가 들어 있는 원본 FBX 의 휴머노이드 아바타를 쓴다.
        private static Avatar FindModelAvatar(GameObject root)
        {
            var skinned = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned == null || skinned.sharedMesh == null)
                return null;

            string modelPath = AssetDatabase.GetAssetPath(skinned.sharedMesh);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar avatar && avatar.isHuman)
                    return avatar;
            }
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static float RendererTop(GameObject go)
        {
            float top = 0f;
            bool found = false;
            foreach (Renderer targetRenderer in go.GetComponentsInChildren<Renderer>())
            {
                if (targetRenderer is SkinnedMeshRenderer || targetRenderer is MeshRenderer)
                {
                    top = found ? Mathf.Max(top, targetRenderer.bounds.max.y) : targetRenderer.bounds.max.y;
                    found = true;
                }
            }
            return top;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Log(string message) => Report.Add(message);
    }
}
