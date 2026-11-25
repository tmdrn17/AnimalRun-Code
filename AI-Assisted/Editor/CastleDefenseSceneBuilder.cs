using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Animal_Run.Scripts.EditorTools
{
    // 기획서(Docs/기획서_v2_성장형_멀티모드.md) 4.2 마을 배치를 Synty PolygonFantasyKingdom 에셋으로 Castle Defense 씬에 깐다.
    // 씬의 "Village" 루트를 지우고 다시 만들므로 여러 번 실행해도 된다. 좌표: +Z = 북쪽(적이 오는 쪽), 카메라는 남쪽에서 내려다본다.
    // 메뉴: Tools/Animal Run/Build Castle Defense Village
    // 외부 요청: Temp/SceneBuildRequests/CastleDefense.request → 같은 폴더에 .result 와 _preview.png
    public static class CastleDefenseSceneBuilder
    {
        public const string ScenePath = "Assets/Animal Run/Scenes/Castle Defense.unity";
        private const string FK = "Assets/Synty/PolygonFantasyKingdom/Prefabs/";
        private const string Preset = FK + "Buildings/Presets/";
        private const string Gen = "Assets/Synty/PolygonGeneric/Prefabs/";


        private static readonly List<string> Report = new List<string>();
        private static int plotIndex;

        [MenuItem("Tools/Animal Run/Build Castle Defense Village")]
        private static void BuildFromMenu()
        {
            if (!OpenTargetScene())
                return;
            Build();
        }

        public static bool OpenTargetScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
                return true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError($"[VillageBuilder] 저장 안 된 씬({SceneManager.GetSceneAt(i).path})이 있어 중단합니다. 저장 후 다시 실행하세요.");
                    return false;
                }
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        public static List<string> Build()
        {
            Report.Clear();
            plotIndex = 0;
            Scene scene = SceneManager.GetActiveScene();

            var old = GameObject.Find("Village");
            if (old != null)
                Object.DestroyImmediate(old);
            var placeholderFloor = GameObject.Find("Cube");
            if (placeholderFloor != null && placeholderFloor.transform.localScale.x > 20f)
                Object.DestroyImmediate(placeholderFloor);

            var root = new GameObject("Village").transform;

            BuildGround(Group(root, "Ground"));
            BuildCastleCore(Group(root, "CastleCore"));
            BuildPlots(Group(root, "BuildPlots"));
            BuildLanes(Group(root, "Lanes"));
            BuildExpedition(Group(root, "Expedition"));
            BuildRestPoints(Group(root, "HeroRestPoints"));
            BuildDecor(Group(root, "Decor"));
            BuildTerrain(Group(root, "Terrain"));
            SetupPlayerAndCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("씬 저장: " + scene.path);
            return new List<string>(Report);
        }

        // ───────────────────────── 지형 정의 ─────────────────────────
        // 마을 = 절벽으로 둘러싼 타원 골짜기. 밖으로 나가는 길은 셋뿐:
        //   북쪽 길목·서쪽 길목(적이 오는 협곡, 처음부터 둘 다 열림) / 남쪽 원정 길(적이 오지 않음, 러시 런 입장).
        // 성벽은 마을을 둘러싸지 않고 두 길목 입구만 막는다. +Z = 북쪽, 카메라는 남쪽에서 내려다본다.
        private const float ValleyA = 34f;   // 골짜기 반폭 (x)
        private const float ValleyB = 29f;   // 골짜기 반깊이 (z)
        private const float ValleyZ = -1f;   // 골짜기 중심 z
        private const float LaneHalf = 8f;   // 길목 반폭 (폭 16m)
        private const float TowerOffset = LaneHalf + 4f; // 망루 칸: 길목 가장자리에서 4m 바깥

        // 길목: 입구(성벽 자리)에서 포털까지 약 32m
        private static readonly Rect LaneNorth = Rect.MinMaxRect(-LaneHalf, 22f, LaneHalf, 66f);   // x, z
        private static readonly Rect LaneWest = Rect.MinMaxRect(-74f, 4f - LaneHalf, -28f, 4f + LaneHalf);
        private static readonly Rect ExpeditionRoad = Rect.MinMaxRect(-3.5f, -44f, 3.5f, -24f);
        private static readonly Vector3 NorthMouth = new Vector3(0, 0, 28f);
        private static readonly Vector3 WestMouth = new Vector3(-33.6f, 0, 4f);
        private static readonly Vector3 NorthPortal = new Vector3(0, 0, 60f);
        private static readonly Vector3 WestPortal = new Vector3(-66f, 0, 4f);
        private static readonly Vector3 ExpeditionGatePos = new Vector3(0, 0, -38f);

        // 걸어 다닐 수 있는 곳까지의 거리(안쪽이면 0 이하)
        private static float DistToOpen(Vector3 p)
        {
            float nx = p.x / ValleyA, nz = (p.z - ValleyZ) / ValleyB;
            float valley = (Mathf.Sqrt(nx * nx + nz * nz) - 1f) * Mathf.Min(ValleyA, ValleyB);
            return Mathf.Min(valley, Mathf.Min(RectDist(LaneNorth, p), Mathf.Min(RectDist(LaneWest, p), RectDist(ExpeditionRoad, p))));
        }

        private static float RectDist(Rect r, Vector3 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0, p.x - r.xMax);
            float dz = Mathf.Max(r.yMin - p.z, 0, p.z - r.yMax);
            if (dx == 0 && dz == 0)
                return -Mathf.Min(Mathf.Min(p.x - r.xMin, r.xMax - p.x), Mathf.Min(p.z - r.yMin, r.yMax - p.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ───────────────────────── 바닥 ─────────────────────────
        private static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Grass";
            ground.transform.SetParent(parent);
            ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300m: 숲 끝까지 덮음
            ground.isStatic = true;
            var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonFantasyKingdom/Materials/Ground/Grass_01.mat");
            if (grass != null)
                ground.GetComponent<Renderer>().sharedMaterial = grass;
            else
                Log("WARN Grass_01.mat 없음");

            // 성 앞 광장
            Place(FK + "Environments/SM_Env_Ground_Flat_Dirt_01.prefab", parent, new Vector3(0, 0.01f, -6), 0, 12f, "Plaza_Dirt");
            // 흙길: 길목 두 개와 원정 길 (겹치게 깔아 끊기지 않게)
            DirtPath(parent, "Path_North", new Vector3(0, 0, 9), NorthPortal);
            DirtPath(parent, "Path_West", new Vector3(-6, 0, 4), WestPortal);
            DirtPath(parent, "Path_Expedition", new Vector3(0, 0, -12), ExpeditionGatePos);
        }

        private static void DirtPath(Transform parent, string name, Vector3 from, Vector3 to)
        {
            var g = Group(parent, name);
            Vector3 d = to - from;
            int n = Mathf.CeilToInt(d.magnitude / 2.5f);
            for (int i = 0; i <= n; i++)
                Place(FK + "Environments/SM_Env_Ground_Flat_Dirt_02.prefab", g, from + d * (i / (float)n) + Vector3.up * 0.01f, i * 37f, 4.2f, "PathDirt");
        }

        // ───────────────────────── 성 중앙 ─────────────────────────
        private static void BuildCastleCore(Transform parent)
        {
            // 부서지면 패배하는 중심 건물. 성탑 4개 + 성벽 + 앞쪽 성문으로 만든 작은 요새(약 11×11m)
            var core = new GameObject("Plot_00_castle").transform;
            core.SetParent(parent);
            Vector3 c = new Vector3(0, 0, 3);
            core.position = c;
            MarkPlot(core.gameObject, "castle", 0);
            const float h = 4.2f;
            string tower = FK + "Castle/SM_Bld_Castle_Wall_Tower_M_01.prefab";
            string wall = FK + "Castle/SM_Bld_Castle_Wall_01.prefab";
            Place(FK + "Castle/SM_Bld_Castle_Floor_Stone_01.prefab", core, c + Vector3.up * 0.02f, 0, h * 2f, "Core_Floor");
            TileLine(core, wall, c + new Vector3(-h, 0, h), c + new Vector3(h, 0, h), "Core_Wall");
            TileLine(core, wall, c + new Vector3(-h, 0, -h), c + new Vector3(-h, 0, h), "Core_Wall");
            TileLine(core, wall, c + new Vector3(h, 0, -h), c + new Vector3(h, 0, h), "Core_Wall");
            Place(FK + "Castle/SM_Bld_Castle_Wall_Gate_01.prefab", core, c + new Vector3(0, 0, -h), 0, h * 2f - 2f, "Core_Gate");
            foreach (var o in new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h) })
                Place(tower, core, c + o, 0, 0, "Core_Tower");
            Place(FK + "Props/Banners/SM_Prop_Flag_01.prefab", core, c + new Vector3(0, 0, 1), 0, 0, "Core_Flag");
            Place(FK + "Props/Banners/SM_Prop_Battle_Banner_01.prefab", core, c + new Vector3(-3.2f, 0, -h - 1.5f), 0, 0, "Core_Banner_L");
            Place(FK + "Props/Banners/SM_Prop_Battle_Banner_01.prefab", core, c + new Vector3(3.2f, 0, -h - 1.5f), 0, 0, "Core_Banner_R");
            AddCoreRoof(core);
            AddBlocker(core.gameObject, core);
        }

        // ───────────────────────── 건설 칸 ─────────────────────────
        // 수입 건물(집·밭·방앗간)은 길목에서 먼 남동쪽, 군사 건물(막사)은 길목 쪽.
        private static void BuildPlots(Transform parent)
        {
            // 생산 (남동쪽 = 길목 반대편)
            Building(parent, "house", Preset + "SM_Bld_Preset_Hut_01_Optimized.prefab", new Vector3(-12, 0, -15), 0, 6f);
            Building(parent, "house", Preset + "SM_Bld_Preset_House_09_A_Optimized.prefab", new Vector3(12, 0, -14), 0, 6.5f);
            Farm(Building(parent, "farm", null, new Vector3(16, 0, -21), 0, 0));
            Farm(Building(parent, "farm", null, new Vector3(27, 0, -11), 0, 0));
            var mill = Building(parent, "mill", Preset + "SM_Bld_Preset_Hut_02_Optimized.prefab", new Vector3(28, 0, 1), 0, 6f);
            Place(FK + "Props/SM_Prop_Windmill_03.prefab", mill, new Vector3(28, 1.1f, 1 - 3.6f), 0, 5f, "MillBlades"); // 기단을 땅속에 묻은 높이 기준

            // 성장 (동쪽·북동쪽)
            var ranch = Building(parent, "ranch", Preset + "SM_Bld_Preset_Shelter_01_Optimized.prefab", new Vector3(20, 0, 9), 0, 6.5f);
            Paddock(ranch, new Vector3(13, 0, 5));
            var forge = Building(parent, "forge", Preset + "SM_Bld_Preset_Shelter_02_Optimized.prefab", new Vector3(22, 0, 19), -90, 6.5f);
            Place(FK + "Props/SM_Prop_Forge_01.prefab", forge, new Vector3(18, 0, 18), 0, 0, "ForgeFurnace");
            Place(FK + "Props/SM_Prop_Anvil_01.prefab", forge, new Vector3(17.5f, 0, 20.5f), 30, 0, "Anvil");
            Building(parent, "heroLodge", Preset + "SM_Bld_Preset_House_07_Optimized.prefab", new Vector3(14, 0, 17), 180, 8f);

            // 군사 (길목 쪽): 사수 Small / 보병 Medium / 돌격 Large
            Building(parent, "barracks_small", FK + "Buildings/SM_Bld_Tent_Open_01.prefab", new Vector3(-9, 0, 18), 0, 4f);
            Building(parent, "barracks_medium", FK + "Buildings/SM_Bld_Tent_Small_01.prefab", new Vector3(-20, 0, 14), 0, 5f);
            Building(parent, "barracks_large", FK + "Buildings/SM_Bld_Tent_Square_01.prefab", new Vector3(-21, 0, -7), 90, 6f);

            // 빈 건설 칸
            EmptyPlot(parent, new Vector3(-11, 0, -5), "any");
            EmptyPlot(parent, new Vector3(-6, 0, -22), "house");
            EmptyPlot(parent, new Vector3(-22, 0, -14), "house");
        }

        private static Transform Building(Transform parent, string id, string prefab, Vector3 pos, float yRot, float footprint)
        {
            var plot = new GameObject($"Plot_{++plotIndex:00}_{id}").transform;
            plot.SetParent(parent);
            plot.position = pos;
            MarkPlot(plot.gameObject, id, plotIndex);
            if (prefab != null)
            {
                var b = Place(prefab, plot, pos, yRot, footprint, id);
                SinkFoundation(b);   // 돌 기단을 땅속으로
                AddBlocker(b, plot); // 적이 부술 대상 = 충돌 박스
            }
            return plot;
        }

        private static void EmptyPlot(Transform parent, Vector3 pos, string allowed)
        {
            var plot = new GameObject($"Plot_{++plotIndex:00}_empty_{allowed}").transform;
            plot.SetParent(parent);
            plot.position = pos;
            // 흙 바닥 + 모서리 횃불로 "지을 수 있는 칸" 표시
            Place(FK + "Environments/SM_Env_Ground_Flat_Dirt_02.prefab", plot, pos + Vector3.up * 0.02f, 0, 4.5f, "PlotGround");
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(i % 2 == 0 ? -2f : 2f, 0, i < 2 ? -2f : 2f);
                Place(FK + "Props/SM_Prop_Torch_01.prefab", plot, pos + c, 0, 0, "PlotCorner");
            }
        }

        private static void MarkPlot(GameObject go, string id, int index)
        {
            // 게임 코드(BuildPlot)가 생기기 전까지 이름 규칙으로 구분: Plot_<번호>_<건물ID>
            Log($"plot {index:00} {id} @ {go.transform.position}");
        }

        private static void Farm(Transform plot)
        {
            Vector3 c = plot.position;
            for (int i = 0; i < 4; i++)
                Place(FK + "Environments/SM_Env_Ground_Farm_Row_01.prefab", plot, c + new Vector3((i - 1.5f) * 1.6f, 0.01f, 0), 0, 0, "FarmRow");
            Place(FK + "Props/SM_Prop_Scarecrow_01.prefab", plot, c + new Vector3(4f, 0, 0), -90, 0, "Scarecrow");
            Place(FK + "Props/SM_Prop_Hay_Bale_01.prefab", plot, c + new Vector3(-4.5f, 0, 2.5f), 30, 0, "HayBale");
        }

        private static void Paddock(Transform plot, Vector3 center)
        {
            FenceLine(plot, center + new Vector3(-3.75f, 0, -2.5f), center + new Vector3(3.75f, 0, -2.5f));
            FenceLine(plot, center + new Vector3(-3.75f, 0, 2.5f), center + new Vector3(3.75f, 0, 2.5f));
            FenceLine(plot, center + new Vector3(-3.75f, 0, -2.5f), center + new Vector3(-3.75f, 0, 2.5f));
            FenceLine(plot, center + new Vector3(3.75f, 0, -2.5f), center + new Vector3(3.75f, 0, 2.5f));
            Place(FK + "Props/SM_Prop_Hay_Bale_01.prefab", plot, center + new Vector3(1.5f, 0, 0.5f), 0, 0, "Hay");
        }

        private static void FenceLine(Transform parent, Vector3 a, Vector3 b)
        {
            TileLine(parent, FK + "Props/SM_Prop_Fence_01.prefab", a, b, "Fence");
        }

        // ───────────────────────── 길목: 성벽 칸 + 망루 칸 2개 + 포털 ─────────────────────────
        private static void BuildLanes(Transform parent)
        {
            // 북쪽 길목: 성벽은 동서로 가로지름
            var north = Group(parent, "Lane_North");
            WallPlot(north, NorthMouth + new Vector3(-LaneHalf - 1, 0, 0), NorthMouth + new Vector3(LaneHalf + 1, 0, 0));
            Building(north, "tower", FK + "Buildings/SM_Bld_Wooden_Tower_01.prefab", NorthMouth + new Vector3(-TowerOffset, 0, -5), 0, 4.3f);
            EmptyPlot(north, NorthMouth + new Vector3(TowerOffset, 0, -5), "tower");
            Portal(north, "EnemyPortal_North", NorthPortal);
            Waypoints(north, NorthPortal, NorthMouth + new Vector3(0, 0, 4), NorthMouth + new Vector3(0, 0, -6), new Vector3(0, 0, 9));

            // 서쪽 길목: 성벽은 남북으로 가로지름
            var west = Group(parent, "Lane_West");
            WallPlot(west, WestMouth + new Vector3(0, 0, -LaneHalf - 1), WestMouth + new Vector3(0, 0, LaneHalf + 1));
            Building(west, "tower", FK + "Buildings/SM_Bld_Wooden_Tower_01.prefab", WestMouth + new Vector3(8.6f, 0, TowerOffset), 0, 4.3f);
            EmptyPlot(west, WestMouth + new Vector3(8.6f, 0, -TowerOffset), "tower");
            Portal(west, "EnemyPortal_West", WestPortal);
            Waypoints(west, WestPortal, WestMouth + new Vector3(-4, 0, 0), WestMouth + new Vector3(6, 0, 0), new Vector3(-6, 0, 3));
        }

        // 성벽 칸: 기본 Lv1 성벽이 지어진 상태(낮은 돌담 + 기둥). 적은 이걸 부숴야 마을로 들어온다
        private static void WallPlot(Transform parent, Vector3 a, Vector3 b)
        {
            var plot = new GameObject($"Plot_{++plotIndex:00}_wall").transform;
            plot.SetParent(parent);
            plot.position = (a + b) * 0.5f;
            MarkPlot(plot.gameObject, "wall", plotIndex);
            BuildGatedWall(plot, a, b);
        }

        // 성벽 = 왼쪽 돌담 + 가운데 문 + 오른쪽 돌담.
        // 문: 단단한 충돌이 없어 대장·영웅·병사는 지나가고, NavMeshObstacle(carve) 이 NavMesh 에 구멍을 내서 NavMesh 로 길을 찾는 적은 못 지나간다.
        // 돌담: 충돌 박스(적이 부술 대상).
        private const float GateWidth = 3.2f;

        private static void BuildGatedWall(Transform plot, Vector3 a, Vector3 b)
        {
            b.y = a.y; // 땅 높이는 a 기준
            Vector3 dir = (b - a).normalized;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 g1 = mid - dir * (GateWidth * 0.5f);
            Vector3 g2 = mid + dir * (GateWidth * 0.5f);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90f; // 로컬 X 를 성벽 방향으로

            var left = Group(plot, "Wall_Left");
            var right = Group(plot, "Wall_Right");
            TileLine(left, FK + "Environments/SM_Env_StoneWall_01.prefab", a, g1, "Wall");
            TileLine(right, FK + "Environments/SM_Env_StoneWall_01.prefab", g2, b, "Wall");
            foreach (var p in new[] { a, g1, g2, b })
                Place(FK + "Environments/SM_Env_StoneWall_Pillar_01.prefab", plot, p, 0, 0, "Wall_Pillar");
            AddBlocker(left.gameObject, left);
            AddBlocker(right.gameObject, right);

            // 문 모양: 나무 울타리 문 (긴 쪽을 성벽 방향에 맞춤)
            string gatePath = FK + "Props/SM_Prop_Fence_Gate_01.prefab";
            var gatePrefab = Load(gatePath);
            if (gatePrefab != null)
            {
                Bounds lb = MeasureLocal(gatePrefab);
                float gateYaw = lb.size.x >= lb.size.z ? yaw : yaw + 90f;
                Place(gatePath, plot, mid, gateYaw, GateWidth - 0.9f, "Gate");
            }
            Place(FK + "Props/SM_Prop_Torch_01.prefab", plot, g1 - Vector3.Cross(dir, Vector3.up) * 0.8f, 0, 0, "Gate_Torch");
            Place(FK + "Props/SM_Prop_Torch_01.prefab", plot, g2 - Vector3.Cross(dir, Vector3.up) * 0.8f, 0, 0, "Gate_Torch");

            var barrier = new GameObject("Gate_EnemyBarrier");
            barrier.transform.SetParent(plot);
            barrier.transform.position = mid + Vector3.up * 1.5f; // mid.y = 땅 높이
            barrier.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var obstacle = barrier.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(GateWidth, 3f, 0.8f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        // 지금 씬의 길목 성벽(Plot_NN_wall)에만 문을 낸다. 씬을 다시 만들지 않으므로 손으로 고친 다른 부분은 그대로 남는다.
        // 양 끝 기둥 위치를 성벽 끝으로 보고, 성벽에 딸린 돌담·기둥·깃발·충돌만 지우고 다시 짓는다.
        [MenuItem("Tools/Animal Run/Add Gates To Lane Walls")]
        private static void AddGatesFromMenu()
        {
            if (OpenTargetScene())
                AddLaneGates();
        }

        // ───────────────────────── 성 지붕 · 건물 기단 ─────────────────────────
        // Synty 프리셋 집은 비탈용 돌 기단이 붙은 통짜 메시라 기단만 뗄 수 없다 → 기단 높이만큼 땅속으로 내린다.
        // 높이는 프리팹 원래 크기 기준(옆모습을 직교 카메라로 재서 얻은 값). 기단 없는 건물(창고 Shelter_02, 천막, 망루)은 목록에 없음.
        private static readonly Dictionary<string, float> FoundationHeights = new Dictionary<string, float>
        {
            { "SM_Bld_Preset_Hut_01_Optimized", 3.8f },
            { "SM_Bld_Preset_Hut_02_Optimized", 3.9f },
            { "SM_Bld_Preset_House_09_A_Optimized", 3.8f },
            { "SM_Bld_Preset_House_07_Optimized", 3.8f },
            { "SM_Bld_Preset_Shelter_01_Optimized", 3.8f },
        };

        private static float FoundationOf(GameObject go)
        {
            if (go == null || !PrefabUtility.IsAnyPrefabInstanceRoot(go)) return 0f;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return FoundationHeights.TryGetValue(Path.GetFileNameWithoutExtension(path), out float h) ? h : 0f;
        }

        // 기단 높이만큼 내린다. 이미 내려가 있으면(바닥이 땅보다 0.5m 이상 아래) 그대로.
        private static bool SinkFoundation(GameObject go, float groundY = 0f)
        {
            float h = FoundationOf(go);
            if (h <= 0f) return false;
            if (RendererBounds(go).min.y < groundY - 0.5f) return false;
            go.transform.position -= Vector3.up * h * go.transform.lossyScale.y;
            return true;
        }

        // 성 중앙: 성벽 위에 낮은 사각 지붕, 성탑 4개 위에 낮은 돔
        private static void AddCoreRoof(Transform core)
        {
            var towers = new List<Bounds>();
            float wallTop = 0f;
            foreach (Transform c in core)
            {
                string n = BaseName(c.name);
                if (n == "Core_Tower") towers.Add(RendererBounds(c.gameObject));
                else if (n == "Core_Wall") wallTop = Mathf.Max(wallTop, RendererBounds(c.gameObject).max.y);
            }
            if (towers.Count == 0) { Log("SKIP 성 지붕: Core_Tower 없음"); return; }
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var t in towers)
            {
                minX = Mathf.Min(minX, t.center.x); maxX = Mathf.Max(maxX, t.center.x);
                minZ = Mathf.Min(minZ, t.center.z); maxZ = Mathf.Max(maxZ, t.center.z);
            }
            if (wallTop <= 0f) wallTop = towers[0].min.y + 5f;
            float span = Mathf.Max(maxX - minX, maxZ - minZ);
            // 지붕 원본(2.4m)을 성벽 안쪽 폭에 맞게 키운다 (Place 의 footprint 는 줄이기만 함)
            var roofPos = new Vector3((minX + maxX) * 0.5f, wallTop - 0.05f, (minZ + maxZ) * 0.5f);
            var roof = Place(FK + "Castle/SM_Bld_Castle_Roof_Cap_Square_01.prefab", core, roofPos, 0, 0, "Core_Roof");
            if (roof != null)
            {
                Bounds rb = RendererBounds(roof);
                roof.transform.localScale *= (span + 0.6f) / Mathf.Max(rb.size.x, rb.size.z);
                Recenter(roof, roofPos);
            }
            foreach (var t in towers)
                Place(FK + "Castle/SM_Bld_Castle_Roof_Cap_Round_01.prefab", core,
                    new Vector3(t.center.x, t.max.y - 0.05f, t.center.z), 0, Mathf.Max(t.size.x, t.size.z) * 1.08f, "Core_TowerCap");
        }

        // 지금 씬에 적용 (다시 만들지 않음): 성 지붕 덮기 + 기단 있는 건물 내리기 + 방앗간 날개 맞추기 + 충돌 박스 다시 맞추기
        [MenuItem("Tools/Animal Run/Roof Castle And Sink Foundations")]
        private static void RoofAndSinkFromMenu()
        {
            if (OpenTargetScene())
                RoofAndSink();
        }

        public static List<string> RoofAndSink()
        {
            Report.Clear();
            Scene scene = SceneManager.GetActiveScene();
            var all = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                all.AddRange(root.GetComponentsInChildren<Transform>(true));

            // 성 지붕
            foreach (var t in all)
            {
                if (t == null || !System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Plot_\d+_castle$")) continue;
                var old = new List<GameObject>();
                foreach (Transform c in t)
                    if (BaseName(c.name) == "Core_Roof" || BaseName(c.name) == "Core_TowerCap") old.Add(c.gameObject);
                foreach (var o in old) Object.DestroyImmediate(o);
                AddCoreRoof(t);
                Log($"{t.name}: 지붕·탑 돔 덮음");
            }

            // 기단 내리기 (씬 어디에 있든 해당 프리셋이면)
            foreach (var t in all)
            {
                if (t == null) continue;
                var go = t.gameObject;
                if (FoundationOf(go) <= 0f) continue;
                Transform plot = t.parent != null && t.parent.name.StartsWith("Plot_") ? t.parent : null;
                float groundY = plot != null ? plot.position.y : 0f;
                bool sunk = SinkFoundation(go, groundY);
                Log(sunk ? $"{PathOf(t)}: 기단 {FoundationOf(go) * t.lossyScale.y:0.0}m 내림" : $"{PathOf(t)}: 이미 내려가 있음");
                if (plot == null) continue;

                if (sunk)
                {
                    // 충돌 박스 다시 맞춤
                    var blockers = new List<GameObject>();
                    foreach (Transform c in plot) if (BaseName(c.name) == "Blocker") blockers.Add(c.gameObject);
                    foreach (var bl in blockers) Object.DestroyImmediate(bl);
                    AddBlocker(go, plot);
                }

                // 방앗간 날개: 5m, 오두막 남쪽(카메라 쪽) 벽 바로 앞, 바닥 +1.1m
                Bounds hut = RendererBounds(go);
                foreach (Transform c in plot)
                {
                    if (BaseName(c.name) != "MillBlades") continue;
                    c.rotation = Quaternion.identity; // 날개 면이 남쪽을 보게
                    Bounds bb = RendererBounds(c.gameObject);
                    float m = Mathf.Max(bb.size.x, bb.size.y);
                    if (Mathf.Abs(m - 5f) > 0.05f) c.localScale *= 5f / m;
                    Recenter(c.gameObject, new Vector3(hut.center.x, groundY + 1.1f, hut.min.z - 0.6f));
                    Log($"{PathOf(c)}: 날개 위치 맞춤");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return new List<string>(Report);
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        // 복제하면 붙는 " (1)" 같은 꼬리를 뗀 이름
        private static string BaseName(string name) => System.Text.RegularExpressions.Regex.Replace(name, @" \(\d+\)$", "");

        // ───────────────────────── 길목 넓히기 (씬을 다시 만들지 않음) ─────────────────────────
        // 지금 LaneHalf 기준으로 길목 주변만 고친다. 길목에서 먼 곳(마을 안 등)에 손으로 고친 내용은 건드리지 않는다.
        //  1) 길목 근처에서 새 길목 안으로 들어온 바위·충돌·나무를 지움
        //  2) 새 길목 가장자리에 빈 곳이 생기면 바위(남쪽은 덤불)를 채움
        //  3) 성벽을 새 폭으로 다시 짓고 문을 길 가운데에 둠 (성벽 선의 위치(북쪽 z, 서쪽 x)는 지금 것을 유지)
        //  4) 망루 칸 2개를 새 길목 가장자리 바깥으로 옮김
        [MenuItem("Tools/Animal Run/Widen Lanes (keep manual edits)")]
        private static void WidenLanesFromMenu()
        {
            if (OpenTargetScene())
                WidenLanes();
        }

        private static float NearLane(Vector3 p) => Mathf.Min(RectDist(LaneNorth, p), RectDist(LaneWest, p));

        public static List<string> WidenLanes()
        {
            Report.Clear();
            Scene scene = SceneManager.GetActiveScene();
            Transform village = GameObject.Find("Village")?.transform;
            if (village == null) { Log("Village 없음"); return new List<string>(Report); }
            Transform ridge = village.Find("Terrain/Ridge"), cols = village.Find("Terrain/RidgeColliders"), forest = village.Find("Terrain/Forest");
            Transform lanes = village.Find("Lanes");

            // 1) 새 길목 안으로 들어온 것 지우기 (길목 근처 12m 안의 것만)
            int removed = 0;
            void Clear(Transform group, float keepDist)
            {
                if (group == null) return;
                var kill = new List<GameObject>();
                foreach (Transform c in group)
                {
                    Bounds b = RendererBounds(c.gameObject);
                    Vector3 p = b.size == Vector3.zero ? c.position : b.center;
                    p.y = 0;
                    if (NearLane(p) < 12f && DistToOpen(p) < keepDist) kill.Add(c.gameObject);
                }
                foreach (var k in kill) Object.DestroyImmediate(k);
                removed += kill.Count;
            }
            Clear(ridge, 1.5f);
            Clear(cols, 1.5f);
            Clear(forest, 8f);

            // 2) 새 가장자리 빈 곳 채우기
            var existing = new List<Vector3>();
            if (ridge != null)
                foreach (Transform c in ridge) { var b = RendererBounds(c.gameObject); existing.Add(new Vector3(b.center.x, 0, b.center.z)); }
            var rng = new System.Random(17);
            string[] cliffs = { FK + "Environments/SM_Env_Rock_Cliff_01.prefab", FK + "Environments/SM_Env_Rock_Cliff_02.prefab", FK + "Environments/SM_Env_Rock_Cliff_03.prefab" };
            int added = 0;
            for (float x = -100; x <= 100; x += 4f)
            for (float z = -70; z <= 90; z += 4f)
            {
                var p = new Vector3(x + Rand(rng, -1, 1), 0, z + Rand(rng, -1, 1));
                float d = DistToOpen(p);
                if (d < 1.5f || d > 7f || NearLane(p) > 12f) continue;
                bool taken = false;
                foreach (var e in existing) if ((e - p).sqrMagnitude < 3.5f * 3.5f) { taken = true; break; }
                if (taken) continue;
                bool southSide = IsSouthOfOpen(p);
                var rock = southSide
                    ? Place(FK + "Environments/SM_Env_Bush_0" + (rng.Next(3) + 1) + ".prefab", ridge, p, rng.Next(360), 0, "RidgeBush", maxHeight: 3.5f)
                    : Place(cliffs[rng.Next(cliffs.Length)], ridge, p, rng.Next(360), 0, "Ridge", maxHeight: Rand(rng, 9f, 15f));
                if (rock == null) continue;
                Bounds rb = RendererBounds(rock);
                BoundaryBox(cols, "RidgeCol", rb.center, new Vector3(rb.size.x * 0.8f, 8f, rb.size.z * 0.8f));
                existing.Add(p);
                added++;
            }
            Log($"lane widen: removed {removed}, added ridge {added}");

            // 3) 성벽 다시 짓기 + 4) 망루 칸 옮기기
            float w = LaneHalf + 1f;
            foreach (var (laneName, isNorth) in new[] { ("Lane_North", true), ("Lane_West", false) })
            {
                Transform lane = lanes?.Find(laneName);
                if (lane == null) { Log("SKIP " + laneName + " 없음"); continue; }
                Transform wall = null;
                var towers = new List<Transform>();
                foreach (Transform c in lane)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(c.name, @"^Plot_\d+_wall$")) wall = c;
                    else if (System.Text.RegularExpressions.Regex.IsMatch(c.name, @"^Plot_\d+_(empty_)?tower$")) towers.Add(c);
                }
                if (wall == null) { Log("SKIP " + laneName + " 성벽 없음"); continue; }

                // 지금 성벽 선 위치 (기둥 평균)
                Vector3 line = Vector3.zero; int n = 0; float ground = 0;
                foreach (Transform c in wall)
                    if (BaseName(c.name) == "Wall_Pillar") { var b = RendererBounds(c.gameObject); line += b.center; ground = b.min.y; n++; }
                if (n == 0) line = wall.position; else line /= n;

                Vector3 a, b2, inward;
                if (isNorth) { a = new Vector3(LaneNorth.center.x - w, ground, line.z); b2 = new Vector3(LaneNorth.center.x + w, ground, line.z); inward = Vector3.back; }
                else { a = new Vector3(line.x, ground, LaneWest.center.y - w); b2 = new Vector3(line.x, ground, LaneWest.center.y + w); inward = Vector3.right; }

                var removable = new HashSet<string> { "Wall", "Wall_Pillar", "Wall_Banner", "Blocker", "Wall_Left", "Wall_Right", "Gate", "Gate_Torch", "Gate_EnemyBarrier" };
                var kill = new List<GameObject>();
                foreach (Transform c in wall) if (removable.Contains(BaseName(c.name))) kill.Add(c.gameObject);
                foreach (var k in kill) Object.DestroyImmediate(k);
                BuildGatedWall(wall, a, b2);
                Log($"{wall.name}: {a} -> {b2} (문 = 길 가운데)");

                // 망루 칸: 성벽 양 끝에서 안쪽(마을 쪽)으로 5m, 가장자리 바깥 4m. 지어진 망루를 앞(서쪽/북쪽 기준 왼쪽)으로
                Vector3 along = (b2 - a).normalized;
                Vector3 mid = (a + b2) * 0.5f;
                float inset = isNorth ? 5f : 8.6f; // 서쪽은 골짜기 모서리가 좁아 더 안쪽으로 (BuildLanes 와 같은 값)
                var spots = new[] { mid - along * TowerOffset + inward * inset, mid + along * TowerOffset + inward * inset };
                towers.Sort((t1, t2) => t1.name.Contains("empty").CompareTo(t2.name.Contains("empty")));
                for (int i = 0; i < towers.Count && i < 2; i++)
                {
                    Vector3 target = isNorth ? spots[i] : spots[1 - i]; // 서쪽: 지어진 망루는 북쪽 끝
                    target.y = towers[i].position.y;
                    Vector3 delta = target - towers[i].position;
                    towers[i].position = target; // 자식(건물·충돌)이 함께 움직임
                    Log($"{towers[i].name} 이동 {delta}");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return new List<string>(Report);
        }

        public static List<string> AddLaneGates()
        {
            Report.Clear();
            Scene scene = SceneManager.GetActiveScene();
            var removable = new HashSet<string> { "Wall", "Wall_Pillar", "Wall_Banner", "Blocker", "Wall_Left", "Wall_Right", "Gate", "Gate_Torch", "Gate_EnemyBarrier" };
            int count = 0;
            // 먼저 성벽 목록을 모은다 (돌면서 자식을 지우면 순회 중인 배열이 깨짐)
            var walls = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Plot_\d+_wall$"))
                    walls.Add(t);
            foreach (var t in walls)
            {
                var pillars = new List<Vector3>();
                foreach (Transform c in t)
                    if (BaseName(c.name) == "Wall_Pillar")
                    {
                        Bounds pb = RendererBounds(c.gameObject);
                        pillars.Add(new Vector3(pb.center.x, pb.min.y, pb.center.z));
                    }
                if (pillars.Count < 2)
                {
                    Log($"SKIP {t.name}: 기둥(Wall_Pillar)이 2개 미만");
                    continue;
                }
                // 가장 멀리 떨어진 두 기둥 = 성벽 양 끝
                Vector3 a = pillars[0], b = pillars[1];
                for (int i = 0; i < pillars.Count; i++)
                for (int j = i + 1; j < pillars.Count; j++)
                    if ((pillars[i] - pillars[j]).sqrMagnitude > (a - b).sqrMagnitude) { a = pillars[i]; b = pillars[j]; }
                var kill = new List<GameObject>();
                foreach (Transform c in t)
                    if (removable.Contains(BaseName(c.name))) kill.Add(c.gameObject);
                foreach (var k in kill) Object.DestroyImmediate(k);

                BuildGatedWall(t, a, b);
                Log($"gate added: {t.name} {a} -> {b}");
                count++;
            }
            Log($"walls with gate: {count}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return new List<string>(Report);
        }

        private static void Waypoints(Transform parent, params Vector3[] points)
        {
            // 적 이동 경로 (포털 → 길목 → 성벽 → 성 중앙 앞)
            var g = Group(parent, "Waypoints");
            for (int i = 0; i < points.Length; i++)
            {
                var w = new GameObject($"WP_{i}").transform;
                w.SetParent(g);
                w.position = points[i];
            }
        }

        private static void Portal(Transform parent, string name, Vector3 pos)
        {
            var portal = new GameObject(name).transform;
            portal.SetParent(parent);
            portal.position = pos;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Place(FK + "Environments/SM_Env_Rock_Chunk_0" + (i % 3 + 1) + ".prefab", portal, pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.5f, i * 45f, 1.6f, "PortalRock");
            }
            Place(FK + "Environments/SM_Env_Ground_Crater_01.prefab", portal, pos + Vector3.up * 0.02f, 0, 0, "PortalGround");
            Place(FK + "FX/FX_Magic_Lights_01.prefab", portal, pos + Vector3.up * 0.5f, 0, 0, "PortalFX", keepPivot: true);
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(portal);
            spawn.position = pos;
        }

        // ───────────────────────── 원정 문 (남쪽, 적이 오지 않는 쪽) ─────────────────────────
        private static void BuildExpedition(Transform parent)
        {
            var gate = Place(FK + "Castle/SM_Bld_Castle_Wall_Gate_01.prefab", parent, ExpeditionGatePos, 0, 6f, "ExpeditionGate");
            AddTrigger(gate, "ExpeditionGate_Trigger", new Vector3(5, 4, 2));
            Place(FK + "Props/SM_Prop_Direction_Sign_01.prefab", parent, ExpeditionGatePos + new Vector3(3.5f, 0, 6), 200, 0, "ExpeditionSign");
            Place(FK + "Props/SM_Prop_Street_Lamp_01.prefab", parent, ExpeditionGatePos + new Vector3(-3, 0, 3), 0, 0, "Lamp");
            Place(FK + "Props/SM_Prop_Street_Lamp_01.prefab", parent, ExpeditionGatePos + new Vector3(3, 0, 3), 0, 0, "Lamp");
            // 문 너머는 막음 (문에 닿으면 러시 런으로 넘어가므로 실제로 지나갈 일은 없음)
            BoundaryBox(parent, "ExpeditionGate_Back", ExpeditionGatePos + new Vector3(0, 3, -2), new Vector3(8, 6, 1));
        }

        // ───────────────────────── 영웅 쉼터 지점 ─────────────────────────
        private static void BuildRestPoints(Transform parent)
        {
            Place(FK + "Props/SM_Prop_Well_01.prefab", parent, new Vector3(6, 0, -8), 0, 0, "Well");
            RestPoint(parent, "Rest_Well", new Vector3(6, 0, -10.5f));
            RestPoint(parent, "Rest_CastleFront", new Vector3(0, 0, -4));
            RestPoint(parent, "Rest_Farm", new Vector3(21, 0, -17));
            RestPoint(parent, "Rest_HeroLodge", new Vector3(12, 0, 11.5f));
            RestPoint(parent, "Rest_Ranch", new Vector3(13, 0, 0));
            RestPoint(parent, "Rest_Forge", new Vector3(17, 0, 15));
        }

        private static void RestPoint(Transform parent, string name, Vector3 pos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent);
            p.position = pos;
        }

        // ───────────────────────── 장식 ─────────────────────────
        private static void BuildDecor(Transform parent)
        {
            Place(FK + "Props/SM_Prop_Street_Lamp_01.prefab", parent, new Vector3(-3, 0, -12), 0, 0, "Lamp");
            Place(FK + "Props/SM_Prop_Street_Lamp_01.prefab", parent, new Vector3(3, 0, -12), 0, 0, "Lamp");
            Place(FK + "Props/SM_Prop_Barrel_Stack_01.prefab", parent, new Vector3(25, 0, 14), 0, 0, "Barrels");
            Place(FK + "Props/SM_Prop_Crate_Stack_01.prefab", parent, new Vector3(-15, 0, 19), 20, 0, "Crates");
            Place(FK + "Props/SM_Prop_Hay_Cart_01.prefab", parent, new Vector3(22, 0, -4), 40, 0, "HayCart");
            Place(FK + "Environments/SM_Env_Flowers_01.prefab", parent, new Vector3(-3, 0, -3), 0, 0, "Flowers");
            Place(FK + "Environments/SM_Env_Flowers_02.prefab", parent, new Vector3(3, 0, -3), 0, 0, "Flowers");
            Place(FK + "Environments/SM_Env_Tree_Round_01.prefab", parent, new Vector3(-26, 0, -20), 0, 0, "Tree");
            Place(FK + "Environments/SM_Env_Tree_Round_02.prefab", parent, new Vector3(4, 0, -26), 0, 0, "Tree");
            Place(FK + "Environments/SM_Env_Tree_Round_03.prefab", parent, new Vector3(30, 0, 12), 0, 0, "Tree");
        }

        // ───────────────────────── 골짜기 벽: 바위 능선 + 바깥 숲 ─────────────────────────
        // 걸을 수 있는 곳 바로 바깥은 바위 능선(충돌 있음), 그 너머 땅 끝까지는 숲.
        // 카메라가 남쪽에서 비스듬히 보므로, 걸을 수 있는 곳보다 남쪽에 있는 바위·나무는 낮게 둬서 대장을 가리지 않게 한다.
        private static void BuildTerrain(Transform parent)
        {
            var rng = new System.Random(5);
            var ridge = Group(parent, "Ridge");
            var colliders = Group(parent, "RidgeColliders");
            var forest = Group(parent, "Forest");
            string[] cliffs = { FK + "Environments/SM_Env_Rock_Cliff_01.prefab", FK + "Environments/SM_Env_Rock_Cliff_02.prefab", FK + "Environments/SM_Env_Rock_Cliff_03.prefab" };
            string[] pines = { FK + "Environments/SM_Env_Tree_Pine_Cluster_01.prefab", FK + "Environments/SM_Env_Tree_Pine_Cluster_02.prefab" };
            string[] rounds = { FK + "Environments/SM_Env_Tree_Round_01.prefab", FK + "Environments/SM_Env_Tree_Round_02.prefab", FK + "Environments/SM_Env_Tree_Round_03.prefab" };
            int rocks = 0, trees = 0;

            // 바위 능선: 4m 격자, 걸을 수 있는 곳에서 0.5~7m 바깥
            for (float x = -100; x <= 100; x += 4f)
            for (float z = -70; z <= 90; z += 4f)
            {
                var p = new Vector3(x + Rand(rng, -1, 1), 0, z + Rand(rng, -1, 1));
                float d = DistToOpen(p);
                if (d < 1.5f || d > 7f) continue;
                bool southSide = IsSouthOfOpen(p);
                // 남쪽(카메라 쪽)은 낮은 덤불, 나머지는 바위 절벽
                var rock = southSide
                    ? Place(FK + "Environments/SM_Env_Bush_0" + (rng.Next(3) + 1) + ".prefab", ridge, p, rng.Next(360), 0, "RidgeBush", maxHeight: 3.5f)
                    : Place(cliffs[rng.Next(cliffs.Length)], ridge, p, rng.Next(360), 0, "Ridge", maxHeight: Rand(rng, 9f, 15f));
                if (rock == null) continue;
                Bounds b = RendererBounds(rock);
                BoundaryBox(colliders, "RidgeCol", b.center, new Vector3(b.size.x * 0.8f, 8f, b.size.z * 0.8f));
                rocks++;
            }

            // 숲: 9m 격자, 땅 끝(±146)까지
            for (float x = -146; x <= 146; x += 9f)
            for (float z = -146; z <= 146; z += 9f)
            {
                var p = new Vector3(x + Rand(rng, -3, 3), 0, z + Rand(rng, -3, 3));
                if (DistToOpen(p) < 8f) continue;
                bool southSide = IsSouthOfOpen(p) && DistToOpen(p) < 40f;
                string tree = southSide ? rounds[rng.Next(rounds.Length)] : pines[rng.Next(pines.Length)];
                Place(tree, forest, p, rng.Next(360), 0, "Tree", maxHeight: southSide ? 8f : 0f);
                trees++;
            }
            Log($"terrain: ridge rocks {rocks}, forest trees {trees}");
        }

        // p 의 북쪽 가까이에 걸을 수 있는 곳이 있으면 true (= 카메라와 대장 사이에 끼는 자리)
        private static bool IsSouthOfOpen(Vector3 p)
        {
            for (float dz = 2f; dz <= 26f; dz += 2f)
                if (DistToOpen(p + new Vector3(0, 0, dz)) <= 0f)
                    return true;
            return false;
        }

        private static void BoundaryBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // 건물·성벽 렌더러 바운드에 맞춘 충돌 박스 (적이 부술 대상, 대장이 못 지나감)
        private static void AddBlocker(GameObject target, Transform parent)
        {
            if (target == null) return;
            Bounds b = RendererBounds(target);
            if (b.size == Vector3.zero) return;
            var go = new GameObject("Blocker");
            go.transform.SetParent(parent);
            go.transform.position = b.center;
            go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(b.size.x, 0.8f), Mathf.Min(b.size.y, 6f), Mathf.Max(b.size.z, 0.8f));
        }

        private static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ───────────────────────── 대장·카메라 ─────────────────────────
        private static void SetupPlayerAndCamera()
        {
            var player = GameObject.Find("Player");
            Vector3 spawn = new Vector3(0, 0, -9);
            if (player != null)
            {
                var col = player.GetComponent<CapsuleCollider>();
                float half = col != null ? col.height * 0.5f * player.transform.localScale.y : 1f;
                player.transform.position = spawn + Vector3.up * half;
            }

            var cam = GameObject.Find("Main Camera");
            if (cam != null)
            {
                // 세로 화면 기준 대장을 따라다니는 시점. 대장 기준 뒤 18, 위 24
                cam.transform.position = spawn + new Vector3(0, 24f, -18f);
                cam.transform.rotation = Quaternion.Euler(52f, 0, 0);
                var c = cam.GetComponent<UnityEngine.Camera>();
                c.fieldOfView = 50f;
                c.farClipPlane = 400f;
            }
        }

        // ───────────────────────── 도우미 ─────────────────────────
        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static void AddTrigger(GameObject go, string name, Vector3 size)
        {
            if (go == null) return;
            var t = new GameObject(name);
            t.transform.SetParent(go.transform.parent);
            t.transform.position = go.transform.position + Vector3.up * size.y * 0.5f;
            var box = t.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
        }

        // a→b 를 프리팹 길이로 채운다. 프리팹은 로컬 X 가 긴 방향이라고 가정하고, 아니면 Z 를 쓴다.
        private static void TileLine(Transform parent, string path, Vector3 a, Vector3 b, string name)
        {
            var prefab = Load(path);
            if (prefab == null) return;
            Bounds local = MeasureLocal(prefab);
            bool alongX = local.size.x >= local.size.z;
            float len = Mathf.Max(0.5f, alongX ? local.size.x : local.size.z);
            Vector3 d = b - a;
            float dist = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(dist / len));
            float step = dist / n;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + (alongX ? -90f : 0f);
            for (int i = 0; i < n; i++)
            {
                var go = Place(path, parent, a + d.normalized * (step * (i + 0.5f)), yaw, 0, name);
                if (go != null && step > len)
                {
                    // 틈이 남으면 길이 방향으로 살짝 늘림
                    var s = go.transform.localScale;
                    if (alongX) s.x *= step / len; else s.z *= step / len;
                    go.transform.localScale = s;
                    Recenter(go, a + d.normalized * (step * (i + 0.5f)));
                }
            }
        }

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private static GameObject Load(string path)
        {
            if (Cache.TryGetValue(path, out var p)) return p;
            p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Log("MISSING " + path);
            Cache[path] = p;
            return p;
        }

        private static Bounds MeasureLocal(GameObject prefab)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            Bounds b = RendererBounds(tmp);
            Object.DestroyImmediate(tmp);
            return b;
        }

        // 프리팹을 놓고, 렌더러 바운드의 바닥 가운데가 pos 에 오게 맞춘다. footprint>0 이면 가로·세로 최대값이 그 안에 들어오게 축소.
        // maxHeight>0 이면 높이도 그 안에 들어오게 축소.
        private static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float footprint, string name, bool keepPivot = false, float maxHeight = 0f)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.isStatic = true;
            if (keepPivot) return go;

            Bounds b = RendererBounds(go);
            if (Reported.Add(path))
                Log($"size {Path.GetFileNameWithoutExtension(path)} = {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0}");
            float scale = 1f;
            if (footprint > 0f)
            {
                float m = Mathf.Max(b.size.x, b.size.z);
                if (m > footprint)
                    scale = footprint / m;
            }
            if (maxHeight > 0f && b.size.y * scale > maxHeight)
                scale = maxHeight / b.size.y;
            if (scale < 1f)
                go.transform.localScale *= scale;
            Recenter(go, pos);
            return go;
        }

        private static void Recenter(GameObject go, Vector3 pos)
        {
            Bounds b = RendererBounds(go);
            if (b.size == Vector3.zero) return;
            go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = default;
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void Log(string msg)
        {
            Report.Add(msg);
            Debug.Log("[VillageBuilder] " + msg);
        }

        // ───────────────────────── 미리보기 ─────────────────────────
        public static void ShotPublic(string path, Vector3 pos, Vector3 euler, float fov) => Shot(path, pos, euler, fov, 1200, 800);

        // 마을 가운데를 가까이서 비스듬히 (성 지붕·집 확인용)
        public static void CaptureClose(string dir)
        {
            Shot(Path.Combine(dir, "Close_center.png"), new Vector3(8, 26, -30), new Vector3(42, -12, 0), 45f, 1400, 900);
            Shot(Path.Combine(dir, "Close_east.png"), new Vector3(14, 22, -26), new Vector3(40, 10, 0), 50f, 1400, 900);
        }

        // 길목 성벽 문을 가까이서 (남쪽 위에서 비스듬히)
        public static void CaptureGatePreviews(string dir)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Plot_\d+_wall$")) continue;
                Bounds b = RendererBounds(t.gameObject);
                Shot(Path.Combine(dir, $"Gate_{t.name}.png"), b.center + new Vector3(0, 11, -11), new Vector3(42, 0, 0), 45f, 900, 700);
            }
        }

        public static void CapturePreviews(string dir)
        {
            Shot(Path.Combine(dir, "CastleDefense_overview.png"), new Vector3(-15, 125, -100), new Vector3(52, 0, 0), 50f, 1600, 1200);
            var cam = GameObject.Find("Main Camera");
            if (cam != null)
                Shot(Path.Combine(dir, "CastleDefense_game.png"), cam.transform.position, cam.transform.eulerAngles, cam.GetComponent<UnityEngine.Camera>().fieldOfView, 720, 1280);
        }

        // 후보 프리팹을 한 장의 격자 이미지로 찍는다 (Temp/SceneBuildRequests/Candidates.request 에 경로를 한 줄씩)
        public static bool SideView; // true 면 정면 옆모습 (기단 높이 재기용)

        public static List<string> CaptureCandidates(string[] paths, string pngPath)
        {
            Report.Clear();
            const int cell = 220, cols = 6;
            int rows = Mathf.CeilToInt(paths.Length / (float)cols);
            var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGB24, false);
            var holder = new GameObject("CandidateHolder");
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i].Trim());
                    if (prefab == null) { Log("MISSING " + paths[i]); continue; }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                    go.transform.position = new Vector3(2000, 0, 2000);
                    Bounds b = RendererBounds(go);
                    Log($"cand {i:00} {Path.GetFileNameWithoutExtension(paths[i])} = {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0}");
                    float r = b.extents.magnitude;
                    Vector3 dir = SideView ? new Vector3(0f, 0.02f, -1f).normalized : new Vector3(-0.6f, 0.55f, -1f).normalized;
                    // 옆모습: 직교 카메라, 셀 높이 = 12m 고정 (1px = 12/220 m) → 바닥은 셀 아래 끝
                    var tex = SideView
                        ? Render(new Vector3(b.center.x, b.min.y + 6f, b.min.z - 20f), Vector3.zero, 0f, cell, cell, 6f)
                        : Render(b.center + dir * r * 2.6f, Quaternion.LookRotation(-dir).eulerAngles, 30f, cell, cell);
                    sheet.SetPixels((i % cols) * cell, (rows - 1 - i / cols) * cell, cell, cell, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(go);
                }
                sheet.Apply();
                File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
                return new List<string>(Report);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(sheet);
            }
        }

        private static void Shot(string path, Vector3 pos, Vector3 euler, float fov, int w, int h)
        {
            var tex = Render(pos, euler, fov, w, h);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Texture2D Render(Vector3 pos, Vector3 euler, float fov, int w, int h, float orthoSize = 0f)
        {
            var go = new GameObject("PreviewCam");
            try
            {
                var cam = go.AddComponent<UnityEngine.Camera>();
                cam.transform.position = pos;
                cam.transform.eulerAngles = euler;
                cam.fieldOfView = fov > 0f ? fov : 30f;
                if (orthoSize > 0f) { cam.orthographic = true; cam.orthographicSize = orthoSize; }
                cam.farClipPlane = 500f;
                cam.nearClipPlane = 0.1f;
                cam.clearFlags = CameraClearFlags.Skybox;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                cam.Render(); // 첫 렌더는 셰이더 준비 전이라 색이 틀어질 때가 있음
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
    }

    // 에디터가 열려 있을 때 외부에서 씬 빌드를 요청하는 통로 (RunnerPrefabRequestWatcher 와 같은 방식)
    [InitializeOnLoad]
    public static class SceneBuildRequestWatcher
    {
        private const string RequestDir = "Temp/SceneBuildRequests";
        private static double nextPoll;

        static SceneBuildRequestWatcher()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || !Directory.Exists(RequestDir))
                return;

            foreach (string requestPath in Directory.GetFiles(RequestDir, "CastleDefense.request"))
            {
                File.Delete(requestPath);
                var lines = new List<string>();
                bool ok = false;
                void Capture(string message, string stack, LogType type)
                {
                    if (type == LogType.Exception || type == LogType.Error)
                        lines.Add($"{type}: {message}");
                }
                Application.logMessageReceived += Capture;
                try
                {
                    if (CastleDefenseSceneBuilder.OpenTargetScene())
                    {
                        lines.AddRange(CastleDefenseSceneBuilder.Build());
                        CastleDefenseSceneBuilder.CapturePreviews(RequestDir);
                        ok = true;
                    }
                }
                catch (System.Exception e)
                {
                    lines.Add("Exception: " + e);
                }
                finally
                {
                    Application.logMessageReceived -= Capture;
                }
                lines.Add(ok ? "RESULT OK" : "RESULT FAIL");
                File.WriteAllLines(Path.Combine(RequestDir, "CastleDefense.result"), lines);
            }

            // 임의 시점 사진: Shot.request 한 줄 = "이름 x y z 피치 요 fov" (도착 위치를 보는 용도)
            foreach (string requestPath in Directory.GetFiles(RequestDir, "Shot.request"))
            {
                string[] reqLines = File.ReadAllLines(requestPath);
                File.Delete(requestPath);
                var lines = new List<string>();
                foreach (var l in reqLines)
                {
                    var p = l.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 7) continue;
                    float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
                    CastleDefenseSceneBuilder.ShotPublic(Path.Combine(RequestDir, $"Shot_{p[0]}.png"), new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), 0), F(6));
                    lines.Add("shot " + p[0]);
                }
                lines.Add("RESULT OK");
                File.WriteAllLines(Path.Combine(RequestDir, "Shot.result"), lines);
            }

            foreach (string requestPath in Directory.GetFiles(RequestDir, "RoofAndSink.request"))
            {
                File.Delete(requestPath);
                List<string> lines;
                try
                {
                    lines = CastleDefenseSceneBuilder.OpenTargetScene() ? CastleDefenseSceneBuilder.RoofAndSink() : new List<string> { "씬 열기 실패" };
                    CastleDefenseSceneBuilder.CapturePreviews(RequestDir);
                    CastleDefenseSceneBuilder.CaptureClose(RequestDir);
                    lines.Add("RESULT OK");
                }
                catch (System.Exception e)
                {
                    lines = new List<string> { "Exception: " + e, "RESULT FAIL" };
                }
                File.WriteAllLines(Path.Combine(RequestDir, "RoofAndSink.result"), lines);
            }

            foreach (string requestPath in Directory.GetFiles(RequestDir, "WidenLanes.request"))
            {
                File.Delete(requestPath);
                List<string> lines;
                try
                {
                    lines = CastleDefenseSceneBuilder.OpenTargetScene() ? CastleDefenseSceneBuilder.WidenLanes() : new List<string> { "씬 열기 실패" };
                    CastleDefenseSceneBuilder.CaptureGatePreviews(RequestDir);
                    CastleDefenseSceneBuilder.CapturePreviews(RequestDir);
                    lines.Add("RESULT OK");
                }
                catch (System.Exception e)
                {
                    lines = new List<string> { "Exception: " + e, "RESULT FAIL" };
                }
                File.WriteAllLines(Path.Combine(RequestDir, "WidenLanes.result"), lines);
            }

            foreach (string requestPath in Directory.GetFiles(RequestDir, "LaneGates.request"))
            {
                File.Delete(requestPath);
                List<string> lines;
                try
                {
                    lines = CastleDefenseSceneBuilder.OpenTargetScene() ? CastleDefenseSceneBuilder.AddLaneGates() : new List<string> { "씬 열기 실패" };
                    CastleDefenseSceneBuilder.CaptureGatePreviews(RequestDir);
                    lines.Add("RESULT OK");
                }
                catch (System.Exception e)
                {
                    lines = new List<string> { "Exception: " + e, "RESULT FAIL" };
                }
                File.WriteAllLines(Path.Combine(RequestDir, "LaneGates.result"), lines);
            }

            foreach (string requestPath in Directory.GetFiles(RequestDir, "Candidates.request"))
            {
                string[] all = File.ReadAllLines(requestPath);
                CastleDefenseSceneBuilder.SideView = System.Array.Exists(all, l => l.Trim() == "#side");
                string[] paths = System.Array.FindAll(all, l => l.Trim().Length > 0 && !l.Trim().StartsWith("#"));
                File.Delete(requestPath);
                List<string> lines;
                try
                {
                    lines = CastleDefenseSceneBuilder.CaptureCandidates(paths, Path.Combine(RequestDir, "Candidates.png"));
                    lines.Add("RESULT OK");
                }
                catch (System.Exception e)
                {
                    lines = new List<string> { "Exception: " + e, "RESULT FAIL" };
                }
                File.WriteAllLines(Path.Combine(RequestDir, "Candidates.result"), lines);
            }
        }
    }
}
