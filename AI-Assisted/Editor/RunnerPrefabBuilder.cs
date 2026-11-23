using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Animal_Run.Scripts.EditorTools
{
    // ArtSource/Blender 의 quirky_animal_kit 로 내보낸 FBX 를 러너 프리팹으로 만든다.
    // FBX 위치 규칙: Assets/Animal Run/Models/Runners/<Name>/<Name>.fbx, 같은 폴더에 T_<Name>.png
    public static class RunnerPrefabBuilder
    {
        private const string ModelsRoot = "Assets/Animal Run/Models/Runners";
        private const string PrefabsRoot = "Assets/Resources/Animal Run/Prefabs/Runners";
        private const string SpritesRoot = "Assets/Resources/Animal Run/Sprites/Runners";
        private const string QuirkyRoot = "Assets/ThirdParty/Quirky Series Ultimate";
        private const string DepthPrepassMaterial = "Assets/Animal Run/Materials/Characters/M_RunnerDepthPrepass.mat";
        private const string TemplateMaterial =QuirkyRoot + "/Mega Pack Vol.3/Farm Vol.2/Materials/M_Horse.mat";
        private const string TemplateController = QuirkyRoot + "/Mega Pack Vol.3/Farm Vol.2/Animations/AC_Horse.controller";
        private const string ScalesShader = "Animal Run/Quirky Scales";
        [MenuItem("Tools/Animal Run/Build Runner Prefab From Selected FBX")]
        private static void BuildFromSelection()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".fbx"))
            {
                Debug.LogError("[RunnerPrefabBuilder] Models/Runners/<Name>/<Name>.fbx 를 선택하세요.");
                return;
            }
            BuildAndVerify(System.IO.Path.GetFileNameWithoutExtension(path));
        }

        // Unity -batchmode -executeMethod Animal_Run.Scripts.EditorTools.RunnerPrefabBuilder.BuildBatch -runner Pig
        public static void BuildBatch()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-runner");
            if (i < 0 || i + 1 >= args.Length)
            {
                Debug.LogError("[RunnerPrefabBuilder] -runner <Name> 인자가 필요합니다.");
                return;
            }
            BuildAndVerify(args[i + 1]);
            // Unity 셰이더 미리보기. Temp 는 배치 모드 종료 때 지워지므로 블렌더 미리보기 폴더에 남긴다
            System.IO.Directory.CreateDirectory("ArtSource/Blender/previews");
            CapturePreview(args[i + 1], $"ArtSource/Blender/previews/{args[i + 1]}_unity.png");
        }

        public static void BuildAndVerify(string name)
        {
            Build(name);
            Verify(name);
        }

        public static GameObject Build(string name)
        {
            string dir = $"{ModelsRoot}/{name}";
            string fbxPath = $"{dir}/{name}.fbx";
            string texPath = $"{dir}/T_{name}.png";
            string matPath = $"{dir}/M_{name}.mat";
            string controllerPath = $"{dir}/AC_{name}.controller";
            string iconPath = $"{SpritesRoot}/{name}_Image.png";
            string prefabPath = $"{PrefabsRoot}/{name}.prefab";

            AssetDatabase.Refresh();

            // 팔레트 텍스처: 원본과 같이 Point 필터. 16x4 라 압축하면 블록 안의 이웃 칸 색이 섞이므로(5아래 칸에 6아래 분홍 등) 무압축
            var texImporter = (TextureImporter)AssetImporter.GetAtPath(texPath);
            texImporter.filterMode = FilterMode.Point;
            texImporter.textureCompression = TextureImporterCompression.Uncompressed;
            texImporter.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            var iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            if (iconImporter != null)
            {
                iconImporter.textureType = TextureImporterType.Sprite;
                iconImporter.spriteImportMode = SpriteImportMode.Single;
                iconImporter.alphaIsTransparency = true;
                iconImporter.SaveAndReimport();
            }

            // 머티리얼: M_Horse 복제 후 텍스처만 교체
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
                AssetDatabase.CopyAsset(TemplateMaterial, matPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            bool transparent = texImporter.DoesSourceTextureHaveAlpha();
            SetTransparent(material, transparent);
            ApplyScales(material, $"{dir}/{name}.scales.json");
            EditorUtility.SetDirty(material);

            // 모델: Generic 리그, 블렌드셰이프, 클립 이름 "Rig|Run" -> "Run"
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            modelImporter.animationType = ModelImporterAnimationType.Generic;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importBlendShapes = true;
            modelImporter.importAnimation = true;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            modelImporter.SaveAndReimport();

            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>())
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            ModelImporterClipAnimation[] clips = modelImporter.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = clip.name != "Death";
            }
            modelImporter.clipAnimations = clips;
            modelImporter.SaveAndReimport();

            Dictionary<string, AnimationClip> clipByName = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().First();

            // 컨트롤러: AC_Horse 복제 후 바디 클립(Idle_A, Run)만 교체. 표정 레이어는 그대로 재사용
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
                AssetDatabase.CopyAsset(TemplateController, controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (clipByName.TryGetValue(child.state.name, out AnimationClip clip))
                        child.state.motion = clip;
                }
            }
            EditorUtility.SetDirty(controller);

            // 프리팹: 모델 인스턴스 + 크기가 가장 비슷한 러너 프리팹과 같은 컴포넌트 구성
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = name;
            if (transparent)
            {
                var skinned = root.GetComponentInChildren<SkinnedMeshRenderer>();
                skinned.sharedMaterials = new[] { material, LoadDepthPrepassMaterial() };
            }
            Bounds newBounds = MeasureAtUnitScale(root);
            var template = FindClosestTemplate(name, newBounds.size, out GameObject templateInstance, out Bounds templateBounds);
            Vector3 ratio = Divide(newBounds.size, templateBounds.size);
            Debug.Log($"[RunnerPrefabBuilder] {name} 크기 {newBounds.size} -> 기준 프리팹 {template.name} {templateBounds.size} (비율 {ratio})");

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
                animator = root.AddComponent<Animator>();
            EditorUtility.CopySerialized(template.GetComponent<Animator>(), animator);
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;

            EditorUtility.CopySerialized(template.GetComponent<Rigidbody>(), root.AddComponent<Rigidbody>());

            // 콜라이더·무기 위치는 루트 기준 좌표로 옮긴 뒤 크기 비율만큼 늘이거나 줄인다 (리그 구조·스케일이 달라도 동작)
            var collider = root.AddComponent<CapsuleCollider>();
            EditorUtility.CopySerialized(template.GetComponent<CapsuleCollider>(), collider);
            collider.center = MapPoint(collider.center, templateBounds, newBounds);
            collider.height *= ratio[collider.direction];
            collider.radius *= (ratio[(collider.direction + 1) % 3] + ratio[(collider.direction + 2) % 3]) / 2f;

            Transform templateWeapon = templateInstance.GetComponent<Runner>().weaponPosition;
            Transform body = root.transform.Find("Rig/root/body");
            var weapon = new GameObject("Weapon Position").transform;
            weapon.SetParent(body, false);
            weapon.position = MapPoint(templateWeapon.position, templateBounds, newBounds);
            weapon.rotation = templateWeapon.rotation;
            weapon.localScale = Divide(templateWeapon.lossyScale, body.lossyScale);
            root.AddComponent<Runner>().weaponPosition = weapon;
            Object.DestroyImmediate(templateInstance);

            root.transform.localScale = template.transform.localScale;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RunnerPrefabBuilder] {prefabPath} 생성");
            return prefab;
        }

        // 팔레트 텍스처에 알파가 있으면(사양서 alpha) Particles/Lit 를 반투명으로. ZWrite 는 켜 둬서 몸 안쪽 면은 가린다.
        private static void SetTransparent(Material material, bool transparent)
        {
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)(transparent ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
            material.SetFloat("_ZWrite", 1);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent : -1;
            if (transparent)
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        [System.Serializable]
        private class ScalesSettings
        {
            public float density = 16f;
            public float strength = 0.35f;
            public float[] slots = new float[8];
        }

        // make_animal.py 가 사양서 scales 로 <Name>.scales.json 을 쓰면 비늘 셰이더로, 없으면 원래 Particles/Lit 로
        private static void ApplyScales(Material material, string settingsPath)
        {
            var settingsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(settingsPath);
            if (settingsAsset == null)
            {
                if (material.shader.name == ScalesShader)
                    material.shader = AssetDatabase.LoadAssetAtPath<Material>(TemplateMaterial).shader;
                return;
            }
            var settings = JsonUtility.FromJson<ScalesSettings>(settingsAsset.text);
            material.shader = Shader.Find(ScalesShader);
            material.SetFloat("_ScaleDensity", settings.density);
            material.SetFloat("_ScaleStrength", settings.strength);
            material.SetVector("_ScaleSlotsA", new Vector4(settings.slots[0], settings.slots[1], settings.slots[2], settings.slots[3]));
            material.SetVector("_ScaleSlotsB", new Vector4(settings.slots[4], settings.slots[5], settings.slots[6], settings.slots[7]));
        }

        // 반투명 러너에 두 번째 머티리얼로 붙여 깊이를 먼저 쓴다 (머리 속 메시가 비치지 않게)
        private static Material LoadDepthPrepassMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DepthPrepassMaterial);
            if (material == null)
            {
                material = new Material(Shader.Find("Animal Run/Runner Depth Prepass"));
                AssetDatabase.CreateAsset(material, DepthPrepassMaterial);
            }
            return material;
        }

        // Runners 폴더의 모든 러너 프리팹(새로 추가한 것 포함) 중 루트 스케일 1 기준 크기가 가장 비슷한 것.
        // templateInstance 는 측정에 쓴 임시 인스턴스로, 호출한 쪽에서 지운다.
        private static GameObject FindClosestTemplate(string excludeName, Vector3 size, out GameObject templateInstance, out Bounds templateBounds)
        {
            GameObject best = null;
            float bestScore = float.MaxValue;
            templateInstance = null;
            templateBounds = default;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var runner = candidate.GetComponent<Runner>();
                if (candidate.name == excludeName || runner == null || runner.weaponPosition == null
                    || candidate.GetComponent<CapsuleCollider>() == null)
                    continue;

                var instance = Object.Instantiate(candidate);
                Bounds bounds = MeasureAtUnitScale(instance);
                float score = 0f;
                for (int axis = 0; axis < 3; axis++)
                    score += Mathf.Abs(Mathf.Log(size[axis] / bounds.size[axis]));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    if (templateInstance != null)
                        Object.DestroyImmediate(templateInstance);
                    templateInstance = instance;
                    templateBounds = bounds;
                }
                else
                {
                    Object.DestroyImmediate(instance);
                }
            }
            return best;
        }

        // 루트를 원점·회전 없음·스케일 1 로 두고 모든 렌더러를 감싸는 범위 (기본 포즈).
        // 날개 본(wing.*)에 붙은 버텍스는 뺀다. 펼친 날개까지 넣으면 몸 폭이 부풀어 콜라이더가 커진다.
        private static Bounds MeasureAtUnitScale(GameObject instance)
        {
            Transform t = instance.transform;
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
            Bounds? bounds = null;
            void Add(Bounds b) { if (bounds is Bounds cur) { cur.Encapsulate(b); bounds = cur; } else bounds = b; }

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r is not SkinnedMeshRenderer skinned || skinned.sharedMesh == null)
                {
                    Add(r.bounds);
                    continue;
                }
                // 애니메이션 없는 인스턴스라 바인드 포즈 그대로: 메시 버텍스를 렌더러 변환으로 옮기면 된다
                Vector3[] vertices = skinned.sharedMesh.vertices;
                BoneWeight[] weights = skinned.sharedMesh.boneWeights;
                Transform[] bones = skinned.bones;
                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (i < weights.Length && bones[weights[i].boneIndex0].name.StartsWith("wing"))
                        continue;
                    Add(new Bounds(toWorld.MultiplyPoint3x4(vertices[i]), Vector3.zero));
                }
            }
            return bounds ?? default;
        }

        // 기준 동물 범위 안의 한 점을 새 동물 범위의 같은 상대 위치로
        private static Vector3 MapPoint(Vector3 point, Bounds from, Bounds to)
        {
            return to.center + Vector3.Scale(point - from.center, Divide(to.size, from.size));
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        public static void Verify(string name)
        {
            string fbxPath = $"{ModelsRoot}/{name}/{name}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsRoot}/{name}.prefab");
            var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = prefab.GetComponent<Animator>();
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();

            Debug.Log($"[Verify] mesh={mesh.name} tris={mesh.sharedMesh.triangles.Length / 3} " +
                      $"blendShapes={mesh.sharedMesh.blendShapeCount} bones={mesh.bones.Length} " +
                      $"material={mesh.sharedMaterial.name} shader={mesh.sharedMaterial.shader.name} texture={mesh.sharedMaterial.GetTexture("_BaseMap")?.name} " +
                      $"bounds={mesh.bounds.size}");
            Debug.Log($"[Verify] clips={string.Join(",", clips.Select(c => $"{c.name}({c.length:F3}s,loop={c.isLooping})"))}");
            foreach (var layer in controller.layers)
                foreach (var s in layer.stateMachine.states.Where(s => !s.state.name.StartsWith("Eyes_")))
                    Debug.Log($"[Verify] layer={layer.name} state={s.state.name} motion={AssetDatabase.GetAssetPath(s.state.motion)}:{s.state.motion?.name}");

            // 표정 클립이 이 메시의 블렌드셰이프를 실제로 가리키는지
            var blink = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{QuirkyRoot}/_Animations/Shapekey/Eyes_Blink.anim");
            bool blinkBound = AnimationUtility.GetCurveBindings(blink)
                .Any(b => b.path == mesh.name && mesh.sharedMesh.GetBlendShapeIndex(b.propertyName.Replace("blendShape.", "")) >= 0);
            Debug.Log($"[Verify] Eyes_Blink -> {mesh.name} blendShape bound={blinkBound}");

            // 게임과 같은 방식으로 Resources 에서 로드
            var runner = Resources.Load<Runner>($"Animal Run/Prefabs/Runners/{name}");
            var sprite = Resources.Load<Sprite>($"Animal Run/Sprites/Runners/{name}_Image");
            Debug.Log($"[Verify] Resources runner={runner != null} weaponPosition={runner?.weaponPosition?.parent?.name} sprite={sprite != null}");
        }

        // 프리팹을 Unity 셰이더 그대로 찍어 PNG 로 (블렌더 미리보기로는 안 보이는 정점색·셰이더 문제 확인용).
        // 왼쪽 3/4 앞, 오른쪽 옆모습을 한 장에.
        public static void CapturePreview(string name, string pngPath, int size = 512)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsRoot}/{name}.prefab");
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject instance = preview.InstantiatePrefabInScene(prefab);
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                    first = false;
                }

                Camera camera = preview.camera;
                camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.25f, 0.25f, 0.27f);
                preview.lights[0].intensity = 1.1f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, -40f, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);
                float distance = bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;

                var sheet = new Texture2D(size * 2, size, TextureFormat.RGBA32, false);
                Vector3[] directions =
                {
                    instance.transform.TransformDirection(new Vector3(-0.7f, 0.35f, 1f)).normalized,
                    instance.transform.TransformDirection(new Vector3(-1f, 0.1f, 0f)).normalized,
                };
                for (int i = 0; i < directions.Length; i++)
                {
                    camera.transform.position = bounds.center + directions[i] * distance;
                    camera.transform.LookAt(bounds.center);
                    preview.BeginStaticPreview(new Rect(0, 0, size, size));
                    preview.Render(true);
                    Texture2D shot = preview.EndStaticPreview();
                    var readable = new RenderTexture(size, size, 0);
                    Graphics.Blit(shot, readable);
                    RenderTexture.active = readable;
                    sheet.ReadPixels(new Rect(0, 0, size, size), i * size, 0);
                    RenderTexture.active = null;
                    readable.Release();
                }
                sheet.Apply();
                System.IO.File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                Debug.Log($"[RunnerPrefabBuilder] 미리보기 {pngPath}");
            }
            finally
            {
                preview.Cleanup();
            }
        }
    }
}
