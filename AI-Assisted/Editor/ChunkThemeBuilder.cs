using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Animal_Run.Scripts.EditorTools
{
    // 테마 청크(First / Medium / Long / Long Coins / Finish)에 Synty 에셋 장식을 입힌다.
    // 기본 청크는 없다. 이미 있는 테마 청크에서 "Theme Decor" 만 지우고 다시 꾸미므로 길이·문·코인·결승선은 그대로 유지되고,
    // 바닥색과 길(|x| < 5) 바깥 장식만 테마마다 다르다. 테마 청크가 아직 없는 새 테마는 다른 테마의 같은 종류 청크를 틀로 쓴다.
    // 장식은 콜라이더·라이트를 빼서 게임 판정에 끼어들지 않는다. 시드가 고정이라 다시 만들어도 같은 배치가 나온다.
    // 메뉴: Tools/Animal Run/Build Themed Chunks
    // 외부 요청: Temp/ChunkThemeRequests/Build.request (빌드 + 미리보기) / Preview.request (미리보기만)
    //           → 같은 폴더에 .result 와 Theme_<이름>.png
    public static class ChunkThemeBuilder
    {
        private const string OutDir = "Assets/Animal Run/Prefabs/Chunks/";
        private const string DecorName = "Theme Decor";
        private const string MaterialDir = "Assets/Animal Run/Materials/Environment/Themes/";
        private const string GroundMaterialPath = "Assets/Animal Run/Materials/Environment/Ground.mat";
        private const string ScenePath = "Assets/Animal Run/Scenes/Animal Run.unity";

        private const float PathHalfWidth = 5f;   // 기본 청크 바닥 폭 10
        private const float PathClearance = 0.3f; // 장식이 길 가장자리에서 떨어지는 최소 거리
        private const float SideGroundWidth = 40f;

        private static readonly List<string> Report = new List<string>();

        // ───────────────────────── 테마 정의 ─────────────────────────

        private class Slot
        {
            public string[] Items;
            public float Spacing;          // z 간격 (Count 가 0일 때)
            public float Count;            // 10m 당 개수 (흩뿌리기용, 0이면 간격 배치)
            public Vector2 X;              // 길 중심에서의 |x| 범위
            public float MaxFootprint;
            public float MaxHeight;
            public bool AlignAlongZ;       // 울타리처럼 긴 쪽을 길 방향으로 눕힌다
            public float Chance = 1f;
            public bool FaceRoad;          // 건물처럼 정면(+z)을 길 쪽으로 돌린다
        }

        private class Theme
        {
            public string Id;
            public string DisplayName;
            public Color PathColor;
            public Color SideColor;
            public Slot[] Slots;
            public string[] StartLandmarks;
            public string[] FinishLandmarks;
            public float LandmarkMaxHeight = 12f;
            public MapTheme.ThemeLighting Lighting;
        }

        private static readonly Theme[] Themes =
        {
            new Theme
            {
                Id = "Kingdom Village", DisplayName = "왕국 마을",
                PathColor = new Color(0.62f, 0.49f, 0.34f), SideColor = new Color(0.40f, 0.60f, 0.27f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "FK:SM_Prop_Fence_01", "FK:SM_Prop_Fence_02" }, Spacing = 3f, X = new Vector2(5.4f, 5.4f), MaxFootprint = 3.2f, MaxHeight = 1.4f, AlignAlongZ = true, Chance = 0.85f },
                    new Slot { Items = new[] { "FK:SM_Prop_Street_Lamp_01", "FK:SM_Prop_Barrel_Stack_01", "FK:SM_Prop_Crate_Stack_01", "FK:SM_Prop_Hay_Bale_01", "FK:SM_Prop_Well_01", "FK:SM_Prop_Direction_Sign_01", "FK:SM_Env_Bush_Flowers_01" }, Spacing = 5f, X = new Vector2(6.5f, 8f), MaxFootprint = 2.5f, MaxHeight = 4.5f },
                    new Slot { Items = new[] { "FK:SM_Prop_Market_Stall_01", "FK:SM_Prop_Market_Stall_03" }, Spacing = 15f, X = new Vector2(8.5f, 9.5f), MaxFootprint = 4f, MaxHeight = 4.5f, FaceRoad = true, Chance = 0.6f },
                    new Slot { Items = new[] { "FK:SM_Bld_Preset_House_01_A_Optimized", "FK:SM_Bld_Preset_House_03_Optimized", "FK:SM_Bld_Preset_House_05_Optimized", "FK:SM_Bld_Preset_Tavern_01_Optimized", "FK:SM_Bld_Preset_Blacksmith_01_Optimized", "FK:SM_Bld_Preset_House_Windmill_01_Optimized" }, Spacing = 10f, X = new Vector2(12.5f, 14f), MaxFootprint = 9f, MaxHeight = 13f, FaceRoad = true },
                    new Slot { Items = new[] { "FK:SM_Env_Tree_Round_01", "FK:SM_Env_Tree_Large_01" }, Spacing = 9f, X = new Vector2(18f, 22f), MaxFootprint = 7f, MaxHeight = 12f },
                    new Slot { Items = new[] { "FK:SM_Env_Flowers_01", "FK:SM_Env_Grass_Tuft_01", "FK:SM_Env_Bush_Small_01" }, Count = 4f, X = new Vector2(6f, 16f), MaxFootprint = 1.5f, MaxHeight = 1.2f },
                },
                StartLandmarks = new[] { "FK:SM_Bld_Preset_Tower_01_Optimized" },
                FinishLandmarks = new[] { "FK:SM_Bld_Preset_Church_01_A_Optimized" },
                LandmarkMaxHeight = 16f,
            },
            new Theme
            {
                Id = "Castle", DisplayName = "왕성 성곽",
                PathColor = new Color(0.46f, 0.45f, 0.48f), SideColor = new Color(0.45f, 0.55f, 0.32f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "FK:SM_Env_StoneWall_01", "FK:SM_Env_StoneWall_02" }, Spacing = 4f, X = new Vector2(5.5f, 5.5f), MaxFootprint = 4.2f, MaxHeight = 1.6f, AlignAlongZ = true },
                    new Slot { Items = new[] { "FK:SM_Prop_Battle_Banner_01", "FK:SM_Prop_Battle_Banner_02", "FK:SM_Prop_Flag_Pole_01", "FK:SM_Prop_Barrel_Arrows_01", "FK:SM_Prop_Statue_Knight_01" }, Spacing = 6f, X = new Vector2(7f, 8f), MaxFootprint = 2.5f, MaxHeight = 5f },
                    // 높은 성벽을 길 따라 이어 붙여 성 안 통로처럼 보이게 한다.
                    new Slot { Items = new[] { "FK:SM_Bld_Castle_Wall_01", "FK:SM_Bld_Castle_Wall_Arrowslit_01", "FK:SM_Bld_Castle_Wall_Window_01" }, Spacing = 5f, X = new Vector2(10.5f, 10.5f), MaxFootprint = 5.2f, MaxHeight = 7f, AlignAlongZ = true },
                    new Slot { Items = new[] { "FK:SM_Bld_Preset_Tower_01_Optimized" }, Spacing = 15f, X = new Vector2(15f, 16f), MaxFootprint = 8f, MaxHeight = 15f, FaceRoad = true, Chance = 0.7f },
                    new Slot { Items = new[] { "FK:SM_Env_Grass_Tuft_01", "FK:SM_Env_Bush_Small_01" }, Count = 3f, X = new Vector2(6f, 16f), MaxFootprint = 1.5f, MaxHeight = 1.2f },
                },
                StartLandmarks = new[] { "FK:SM_Bld_Castle_Wall_Tower_M_01" },
                FinishLandmarks = new[] { "FK:SM_Bld_Preset_Tower_01_Optimized" },
                LandmarkMaxHeight = 16f,
            },
            new Theme
            {
                Id = "Viking Snow", DisplayName = "눈 덮인 바이킹 마을",
                PathColor = new Color(0.52f, 0.43f, 0.36f), SideColor = new Color(0.90f, 0.93f, 0.97f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "VK:SM_Prop_Fence_Wood_01_Snow" }, Spacing = 3f, X = new Vector2(5.4f, 5.4f), MaxFootprint = 3.2f, MaxHeight = 1.4f, AlignAlongZ = true, Chance = 0.8f },
                    new Slot { Items = new[] { "VK:SM_Prop_TorchStick_01", "VK:SM_Prop_Rock_Totem_01", "VK:SM_Prop_Fish_Rack_01", "VK:SM_Prop_Rack_01", "VK:SM_Env_Rock_01_Snow" }, Spacing = 5f, X = new Vector2(6.5f, 8f), MaxFootprint = 2.5f, MaxHeight = 4f },
                    new Slot { Items = new[] { "VK:SM_Bld_Guard_Tower_01_Snow", "VK:SM_Bld_Tower_01_Snow", "VK:SM_Prop_Boat_Broken_01" }, Spacing = 13f, X = new Vector2(11.5f, 13f), MaxFootprint = 8f, MaxHeight = 12f, FaceRoad = true },
                    new Slot { Items = new[] { "VK:SM_Env_Tree_Pine_01_Snow", "VK:SM_Env_Tree_Pine_Large_01_Snow" }, Spacing = 5f, X = new Vector2(15f, 22f), MaxFootprint = 6f, MaxHeight = 13f },
                },
                StartLandmarks = new[] { "VK:SM_Env_Stone_Statue_01" },
                FinishLandmarks = new[] { "VK:SM_Bld_Guard_Tower_01_Snow" },
            },
            new Theme
            {
                Id = "Magic Forest", DisplayName = "마법의 숲",
                PathColor = new Color(0.50f, 0.40f, 0.28f), SideColor = new Color(0.27f, 0.47f, 0.24f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "GN:SM_Gen_Env_Bush_01", "FK:SM_Env_Fern_01" }, Spacing = 2.5f, X = new Vector2(5.6f, 6f), MaxFootprint = 2.2f, MaxHeight = 1.3f, Chance = 0.8f },
                    new Slot { Items = new[] { "GN:SM_Gen_Env_Mushroom_01*2.5", "GN:SM_Gen_Env_Log_01", "GN:SM_Gen_Env_Stump_01", "GN:SM_Gen_Env_Rock_01" }, Spacing = 4.5f, X = new Vector2(7f, 9f), MaxFootprint = 3f, MaxHeight = 3.5f },
                    new Slot { Items = new[] { "FK:SM_Env_Tree_Large_01", "GN:SM_Gen_Env_Tree_01", "FK:SM_Env_Tree_Round_01" }, Spacing = 6f, X = new Vector2(11f, 14f), MaxFootprint = 8f, MaxHeight = 13f },
                    new Slot { Items = new[] { "GN:SM_Gen_Env_Tree_01", "FK:SM_Env_Tree_Large_01" }, Spacing = 7f, X = new Vector2(17f, 23f), MaxFootprint = 8f, MaxHeight = 14f },
                    new Slot { Items = new[] { "GN:SM_Gen_Env_Grass_01", "GN:SM_Gen_Env_Flowers_01", "FK:SM_Env_Flowers_01" }, Count = 6f, X = new Vector2(6f, 16f), MaxFootprint = 1.5f, MaxHeight = 1.2f },
                },
                StartLandmarks = new[] { "FK:SM_Env_Big_Tree_01" },
                FinishLandmarks = new[] { "FK:SM_Env_Big_Tree_01" },
                LandmarkMaxHeight = 16f,
            },
            // 할로윈 밤의 으스스한 카니발: 보랏빛 밤, 안개, 호박 랜턴과 전구, 해골·거미·광대.
            new Theme
            {
                Id = "Carnival", DisplayName = "할로윈 카니발",
                PathColor = new Color(0.50f, 0.38f, 0.44f), SideColor = new Color(0.27f, 0.23f, 0.32f),
                Slots = new[]
                {
                    // 길 가장자리: 낡은 울타리와 바리케이드
                    new Slot { Items = new[] { "HC:SM_Bld_Fence_01", "HC:SM_Prop_Barricade_01", "HC:SM_Prop_Barricade_02" }, Spacing = 3f, X = new Vector2(5.4f, 5.4f), MaxFootprint = 3.2f, MaxHeight = 1.4f, AlignAlongZ = true, Chance = 0.85f },
                    // 길 바로 옆(화면에 가장 잘 보임): 호박 랜턴과 가로등을 자주, 으스스한 소품을 섞어서
                    new Slot { Items = new[] { "HC:SM_Prop_Pumpkin_Lantern_01", "HC:SM_Prop_Pumpkin_Lantern_02", "HC:SM_Prop_Pumpkin_Lantern_03" }, Spacing = 3.5f, X = new Vector2(6f, 6.6f), MaxFootprint = 1.2f, MaxHeight = 1.2f, Chance = 0.8f },
                    new Slot { Items = new[] { "HC:SM_Prop_Lamp_Post_01", "HC:SM_Prop_Lamp_Post_02", "HC:SM_Prop_Light_Pole_01" }, Spacing = 8f, X = new Vector2(7f, 7.4f), MaxFootprint = 2f, MaxHeight = 5f },
                    new Slot { Items = new[] { "HC:SM_Prop_Skeleton_01", "HC:SM_Prop_Hay_Bale_Round_01", "HC:SM_Prop_Hay_Bale_Square_01", "HC:SM_Prop_Clown_Head_01*1.5", "HC:SM_Prop_Spider_01*2", "HC:SM_Prop_Trunk_01", "HC:SM_Prop_Jack_In_A_Box_01*1.5", "HC:SM_Prop_Laughing_Clown_01" }, Spacing = 5f, X = new Vector2(8f, 9.5f), MaxFootprint = 2.5f, MaxHeight = 3f },
                    // 뒤쪽: 천막·노점·놀이기구
                    new Slot { Items = new[] { "HC:SM_Prop_Tent_01", "HC:SM_Prop_Tent_Large_01", "HC:SM_Prop_Stall_01_Damaged_01", "HC:SM_Prop_Stall_02_Damaged_01", "HC:SM_Prop_Ticket_Booth_01", "HC:SM_Prop_Merry_Go_Round_01", "HC:SM_Veh_Wagon_Cage_01", "HC:SM_Prop_High_Striker_01" }, Spacing = 10f, X = new Vector2(12.5f, 14.5f), MaxFootprint = 9f, MaxHeight = 10f, FaceRoad = true },
                    // 가장 뒤: 앙상한 죽은 나무
                    new Slot { Items = new[] { "GN:SM_Gen_Env_Tree_Dead_01", "GN:SM_Gen_Env_Tree_Dead_02", "FK:SM_Env_Tree_Dead_01" }, Spacing = 7f, X = new Vector2(18f, 23f), MaxFootprint = 7f, MaxHeight = 11f, Chance = 0.8f },
                    // 바닥: 낙엽 더미, 쓰레기, 작은 호박
                    new Slot { Items = new[] { "HC:SM_Env_Leaves_Pile_01", "HC:SM_Prop_Rubbish_Pile_01", "HC:SM_Prop_Pumpkin_Plastic_01", "HC:SM_Prop_Skull_01*2" }, Count = 3f, X = new Vector2(6.5f, 16f), MaxFootprint = 1.5f, MaxHeight = 1f },
                },
                StartLandmarks = new[] { "HC:SM_Prop_Carnival_Entrance_01" },
                FinishLandmarks = new[] { "HC:SM_Prop_Ferris_Wheel_01" },
                LandmarkMaxHeight = 18f,
                Lighting = new MapTheme.ThemeLighting
                {
                    enabled = true,
                    // 밤 분위기는 내되 러너·코인·문이 잘 보이게 너무 어둡지 않게 한다.
                    sunColor = new Color(0.78f, 0.70f, 1f), sunIntensity = 1.05f, // 보랏빛 달빛
                    ambientColor = new Color(0.52f, 0.44f, 0.66f),
                    useFog = true, fogColor = new Color(0.16f, 0.10f, 0.24f), fogStart = 45f, fogEnd = 120f,
                    useSolidBackground = true, backgroundColor = new Color(0.16f, 0.10f, 0.24f),
                },
            },
            // 엘프 왕국: 밝은 돌길, 생울타리, 가로등·석상·화단, 정자·천문대, 빛나는 나무. 황금빛 오후.
            new Theme
            {
                Id = "Elven Realm", DisplayName = "엘프 왕국",
                PathColor = new Color(0.78f, 0.74f, 0.66f), SideColor = new Color(0.42f, 0.62f, 0.30f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "ER:SM_Env_Hedge_Bush_01", "ER:SM_Env_Hedge_Bush_02", "ER:SM_Env_Hedge_Bush_03" }, Spacing = 3f, X = new Vector2(5.6f, 5.6f), MaxFootprint = 3.2f, MaxHeight = 1.3f, AlignAlongZ = true, Chance = 0.9f },
                    new Slot { Items = new[] { "ER:SM_Prop_Lamp_Post_01", "ER:SM_Prop_Lamp_Post_02", "ER:SM_Prop_LanternStand_01" }, Spacing = 8f, X = new Vector2(6.6f, 7f), MaxFootprint = 2f, MaxHeight = 4.5f },
                    new Slot { Items = new[] { "ER:SM_Prop_Statue_01", "ER:SM_Prop_Statue_02", "ER:SM_Prop_Statue_03", "ER:SM_Prop_Flowerbed_01", "ER:SM_Prop_Flowerbed_02", "ER:SM_Prop_BirdBath_01", "ER:SM_Prop_Sun_Dial_01", "ER:SM_Prop_Pot_Plant_01", "ER:SM_Prop_Bench_Seat_01" }, Spacing = 5f, X = new Vector2(8f, 9.5f), MaxFootprint = 2.5f, MaxHeight = 3.5f },
                    new Slot { Items = new[] { "ER:SM_Bld_Gazebo_01", "ER:SM_Bld_Gazebo_02", "ER:SM_Bld_Planetarium_01", "ER:SM_Prop_Garden_Hut_01", "ER:SM_Prop_Fountain_01", "ER:SM_Prop_Tent_01" }, Spacing = 11f, X = new Vector2(12.5f, 14.5f), MaxFootprint = 9f, MaxHeight = 11f, FaceRoad = true },
                    new Slot { Items = new[] { "ER:SM_Env_Tree_01", "ER:SM_Env_Tree_02", "ER:SM_Env_Tree_03", "ER:SM_Env_Tree_Glowing_01" }, Spacing = 7f, X = new Vector2(17f, 23f), MaxFootprint = 8f, MaxHeight = 13f },
                    new Slot { Items = new[] { "ER:SM_Env_Flowers_01", "ER:SM_Env_Flowers_02", "ER:SM_Env_Flowers_03", "ER:SM_Env_Flowers_Clump_01", "ER:SM_Env_Grass_01" }, Count = 5f, X = new Vector2(6f, 16f), MaxFootprint = 1.5f, MaxHeight = 1.2f },
                },
                StartLandmarks = new[] { "ER:SM_Env_Tree_Glowing_01" },
                FinishLandmarks = new[] { "ER:SM_Bld_Planetarium_01" },
                LandmarkMaxHeight = 15f,
                Lighting = new MapTheme.ThemeLighting
                {
                    enabled = true,
                    sunColor = new Color(1f, 0.90f, 0.72f), sunIntensity = 1.15f, // 황금빛 오후 햇살
                    ambientColor = new Color(0.62f, 0.60f, 0.55f),
                    useFog = true, fogColor = new Color(0.95f, 0.86f, 0.72f), fogStart = 60f, fogEnd = 160f,
                },
            },
            // 요정의 숲: 거목·나무집·포털 나무, 빛나는 크리스털과 거대 버섯, 랜턴. 청록빛 황혼과 안개.
            new Theme
            {
                Id = "Enchanted Grove", DisplayName = "요정의 숲",
                PathColor = new Color(0.56f, 0.50f, 0.38f), SideColor = new Color(0.22f, 0.38f, 0.32f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "EF:SM_Env_Undergrowth_Fern_01", "EF:SM_Env_Undergrowth_Fern_02", "EF:SM_Env_Moss_Lumps_01", "EF:SM_Env_Moss_Lumps_02" }, Spacing = 2.5f, X = new Vector2(5.6f, 6f), MaxFootprint = 2.2f, MaxHeight = 1.2f, Chance = 0.85f },
                    new Slot { Items = new[] { "EF:SM_Prop_Lantern_Stand_01", "EF:SM_Prop_Lantern_Stand_02", "EF:SM_Prop_Lantern_Stand_03" }, Spacing = 7f, X = new Vector2(6.6f, 7f), MaxFootprint = 1.8f, MaxHeight = 3.5f },
                    new Slot { Items = new[] { "EF:SM_Prop_Crystal_01", "EF:SM_Prop_Crystal_02", "EF:SM_Prop_Crystal_03", "EF:SM_Prop_Crystal_04", "EF:SM_Env_Mushroom_Cap_01", "EF:SM_Env_Mushroom_Cap_02", "EF:SM_Env_Mushroom_01", "EF:SM_Env_Mushroom_02", "EF:SM_Prop_Geode_01", "EF:SM_Prop_Ruins_01", "EF:SM_Prop_Ruins_02" }, Spacing = 4.5f, X = new Vector2(7.5f, 9.5f), MaxFootprint = 3f, MaxHeight = 4f },
                    new Slot { Items = new[] { "EF:SM_Env_Tree_Large_01", "EF:SM_Env_Tree_Large_02", "EF:SM_Env_Tree_Medium_01", "EF:SM_Env_Tree_Medium_02", "EF:SM_Env_Fern_Tree_01", "EF:SM_Env_Roots_Cluster_01" }, Spacing = 6f, X = new Vector2(11f, 14f), MaxFootprint = 8f, MaxHeight = 14f },
                    new Slot { Items = new[] { "EF:SM_Env_Tree_Giant_01", "EF:SM_Env_Tree_Giant_02", "EF:SM_Env_Tree_House_01" }, Spacing = 12f, X = new Vector2(19f, 24f), MaxFootprint = 12f, MaxHeight = 20f, Chance = 0.8f },
                    new Slot { Items = new[] { "EF:SM_Env_Mushroom_Small_Group_01", "EF:SM_Env_Mushroom_Small_Group_02", "EF:SM_Env_Mushroom_Small_Group_03", "EF:SM_Env_Undergrowth_Fern_03" }, Count = 5f, X = new Vector2(6f, 16f), MaxFootprint = 1.5f, MaxHeight = 1f },
                },
                StartLandmarks = new[] { "EF:SM_Prop_Archway_01" },
                FinishLandmarks = new[] { "EF:SM_Env_Tree_Portal_01" },
                LandmarkMaxHeight = 16f,
                Lighting = new MapTheme.ThemeLighting
                {
                    enabled = true,
                    sunColor = new Color(0.62f, 0.82f, 0.95f), sunIntensity = 0.95f, // 청록빛 황혼
                    ambientColor = new Color(0.44f, 0.54f, 0.60f),
                    useFog = true, fogColor = new Color(0.13f, 0.24f, 0.28f), fogStart = 42f, fogEnd = 115f,
                    useSolidBackground = true, backgroundColor = new Color(0.13f, 0.24f, 0.28f),
                },
            },
            // 안개 늪: 진흙길, 갈대, 횃불, 무덤·해골, 이끼 낀 폐허 기둥, 늪 나무와 버드나무. 초록빛 안개.
            new Theme
            {
                Id = "Misty Swamp", DisplayName = "안개 늪",
                PathColor = new Color(0.42f, 0.36f, 0.27f), SideColor = new Color(0.27f, 0.33f, 0.21f),
                Slots = new[]
                {
                    new Slot { Items = new[] { "NT:SM_Plant_Reeds_01", "NT:SM_Plant_Reeds_02" }, Spacing = 2.5f, X = new Vector2(5.6f, 6f), MaxFootprint = 2f, MaxHeight = 1.5f, Chance = 0.8f },
                    new Slot { Items = new[] { "NT:SM_Prop_TorchStick_01" }, Spacing = 6f, X = new Vector2(6.4f, 6.8f), MaxFootprint = 1f, MaxHeight = 2.5f },
                    new Slot { Items = new[] { "NT:SM_Prop_Grave_03", "NT:SM_Prop_Skeleton_Ground_01", "NT:SM_Prop_Skull_01*2", "NT:SM_Plant_Mushrooms_01*1.5", "NT:SM_Plant_Mushrooms_02*1.5", "NT:SM_Tree_Swamp_Stump_01", "NT:SM_Tree_Swamp_Stump_02", "NT:SM_Rock_01", "NT:SM_Tree_Swamp_Root_01" }, Spacing = 3.2f, X = new Vector2(7.2f, 9.5f), MaxFootprint = 2.8f, MaxHeight = 2.5f },
                    new Slot { Items = new[] { "NT:SM_Prop_Pillar_Arch_Broken_Moss_01", "NT:SM_Prop_Pillar_Broken_Moss_01", "NT:SM_Tree_Swamp_01", "NT:SM_Tree_Swamp_02", "NT:SM_Tree_Swamp_03", "NT:SM_Tree_Dead_01" }, Spacing = 7f, X = new Vector2(11f, 14f), MaxFootprint = 8f, MaxHeight = 12f },
                    new Slot { Items = new[] { "NT:SM_Tree_Willow_Large_01", "NT:SM_Tree_Willow_Medium_01", "NT:SM_Tree_Swamp_04" }, Spacing = 9f, X = new Vector2(18f, 24f), MaxFootprint = 12f, MaxHeight = 14f },
                    new Slot { Items = new[] { "NT:SM_Terrain_Swamp_Growth_01", "NT:SM_Terrain_Swamp_Growth_02", "NT:SM_Plant_Undergrowth_01", "NT:SM_Plant_Fern_01" }, Count = 4f, X = new Vector2(6f, 16f), MaxFootprint = 1.8f, MaxHeight = 1f },
                },
                StartLandmarks = new[] { "NT:SM_Prop_Pillar_Arch_Moss_01" },
                FinishLandmarks = new[] { "NT:SM_Tree_Willow_Large_01" },
                LandmarkMaxHeight = 14f,
                Lighting = new MapTheme.ThemeLighting
                {
                    enabled = true,
                    sunColor = new Color(0.72f, 0.82f, 0.62f), sunIntensity = 0.9f, // 안개 낀 흐린 빛
                    ambientColor = new Color(0.46f, 0.52f, 0.42f),
                    useFog = true, fogColor = new Color(0.33f, 0.39f, 0.31f), fogStart = 28f, fogEnd = 100f,
                    useSolidBackground = true, backgroundColor = new Color(0.33f, 0.39f, 0.31f),
                },
            },
        };

        // 테마 청크 이름 꼬리 ("<테마 Id> <꼬리>.prefab")
        private static readonly string[] ChunkSuffixes =
        {
            "First Chunk",
            "Medium Chunk",
            "Long Chunk",
            "Long Chunk Coins",
            "Finish Chunk",
        };

        // ───────────────────────── 빌드 ─────────────────────────

        [MenuItem("Tools/Animal Run/Build Themed Chunks")]
        private static void BuildFromMenu()
        {
            foreach (string line in Build())
                Debug.Log("[ChunkThemeBuilder] " + line);
        }

        public static List<string> Build()
        {
            Report.Clear();
            Cache.Clear();
            EnsureFolder(OutDir.TrimEnd('/'));
            EnsureFolder(MaterialDir.TrimEnd('/'));

            var themeAssets = new List<MapTheme>();
            foreach (Theme theme in Themes)
                themeAssets.Add(BuildTheme(theme));

            AssetDatabase.SaveAssets();
            AssignToScene(themeAssets);
            return new List<string>(Report);
        }

        private static MapTheme BuildTheme(Theme theme)
        {
            string dir = OutDir + theme.Id;
            EnsureFolder(dir);

            Material pathMaterial = ThemeMaterial(theme.Id + " Path", theme.PathColor);
            Material sideMaterial = ThemeMaterial(theme.Id + " Side", theme.SideColor);

            var built = new Dictionary<string, Chunk>();
            foreach (string suffix in ChunkSuffixes)
            {
                string chunkName = theme.Id + " " + suffix;
                GameObject template = FindTemplateChunk(theme, suffix);
                if (template == null)
                {
                    Log($"MISSING 틀로 쓸 '{suffix}' 테마 청크가 하나도 없음");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
                try
                {
                    // 틀과의 프리팹 링크를 풀고 예전 장식을 지운다. 안쪽 중첩 프리팹(문·코인 등)은 링크를 유지한다.
                    while (PrefabUtility.IsOutermostPrefabInstanceRoot(instance))
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    Transform oldDecor = instance.transform.Find(DecorName);
                    if (oldDecor != null)
                        Object.DestroyImmediate(oldDecor.gameObject);

                    instance.name = chunkName;
                    instance.transform.position = Vector3.zero;
                    instance.transform.rotation = Quaternion.identity;

                    float length = instance.GetComponent<Chunk>().GetLength();
                    SetPathMaterial(instance, pathMaterial);

                    Transform decor = new GameObject(DecorName).transform;
                    decor.SetParent(instance.transform, false);
                    BuildSideGround(decor, length, sideMaterial);

                    var rng = new System.Random(StableHash(chunkName));
                    foreach (Slot slot in theme.Slots)
                        FillSlot(decor, slot, length, rng);

                    if (suffix == "First Chunk")
                        PlaceLandmarks(decor, theme.StartLandmarks, length / 2f - 4f, theme.LandmarkMaxHeight, rng);
                    else if (suffix == "Finish Chunk")
                        PlaceLandmarks(decor, theme.FinishLandmarks, FinishLineZ(instance) + 3f, theme.LandmarkMaxHeight, rng);

                    string path = $"{dir}/{chunkName}.prefab";
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                    built[suffix] = saved.GetComponent<Chunk>();
                    Log($"{chunkName}: 장식 {decor.childCount - 2}개 → {path}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            string assetPath = $"{dir}/{theme.Id} Theme.asset";
            var mapTheme = AssetDatabase.LoadAssetAtPath<MapTheme>(assetPath);
            if (mapTheme == null)
            {
                mapTheme = ScriptableObject.CreateInstance<MapTheme>();
                AssetDatabase.CreateAsset(mapTheme, assetPath);
            }

            built.TryGetValue("First Chunk", out Chunk first);
            built.TryGetValue("Finish Chunk", out Chunk finish);
            var randoms = new List<Chunk>();
            foreach (string key in new[] { "Long Chunk", "Long Chunk Coins", "Medium Chunk" })
            {
                if (built.TryGetValue(key, out Chunk chunk))
                    randoms.Add(chunk);
            }

            mapTheme.EditorSetup(theme.DisplayName, first, randoms.ToArray(), finish, theme.Lighting);
            EditorUtility.SetDirty(mapTheme);
            return mapTheme;
        }

        // 이 테마의 기존 청크를 먼저 쓰고, 없으면(새 테마) 다른 테마의 같은 종류 청크를 쓴다.
        private static GameObject FindTemplateChunk(Theme theme, string suffix)
        {
            var own = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutDir}{theme.Id}/{theme.Id} {suffix}.prefab");
            if (own != null)
                return own;

            foreach (Theme other in Themes)
            {
                var chunk = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutDir}{other.Id}/{other.Id} {suffix}.prefab");
                if (chunk != null)
                    return chunk;
            }
            return null;
        }

        private static void FillSlot(Transform parent, Slot slot, float length, System.Random rng)
        {
            float half = length / 2f;

            if (slot.Count > 0f)
            {
                int count = Mathf.RoundToInt(slot.Count * length / 10f);
                for (int i = 0; i < count; i++)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float z = Rand(rng, -half + 0.5f, half - 0.5f);
                    PlaceSlotItem(parent, slot, side, z, rng);
                }
                return;
            }

            foreach (float side in new[] { -1f, 1f })
            {
                // 양쪽이 똑같아 보이지 않게 시작 위치를 어긋낸다.
                float z = -half + slot.Spacing * (float)(0.3 + rng.NextDouble() * 0.5);
                while (z < half - 0.2f)
                {
                    if (rng.NextDouble() <= slot.Chance)
                        PlaceSlotItem(parent, slot, side, z, rng);

                    z += slot.AlignAlongZ ? slot.Spacing : slot.Spacing * Rand(rng, 0.8f, 1.2f);
                }
            }
        }

        private static void PlaceSlotItem(Transform parent, Slot slot, float side, float z, System.Random rng)
        {
            string item = slot.Items[rng.Next(slot.Items.Length)];
            float x = side * Rand(rng, slot.X.x, slot.X.y);

            float yaw;
            if (slot.AlignAlongZ)
                yaw = 0f;
            else if (slot.FaceRoad)
                yaw = side < 0f ? 90f : -90f; // 정면(+z)이 길(x=0) 쪽을 보게
            else
                yaw = Rand(rng, 0f, 360f);

            Place(item, parent, new Vector3(x, 0f, z), yaw, slot.MaxFootprint, slot.MaxHeight, slot.AlignAlongZ, side);
        }

        private static void PlaceLandmarks(Transform parent, string[] items, float z, float maxHeight, System.Random rng)
        {
            if (items == null || items.Length == 0)
                return;

            foreach (float side in new[] { -1f, 1f })
            {
                string item = items[rng.Next(items.Length)];
                Place(item, parent, new Vector3(side * 10f, 0f, z), side < 0f ? 90f : -90f, 10f, maxHeight, false, side);
            }
        }

        // 프리팹을 놓고 바운드 바닥 가운데를 pos 에 맞춘다. 길 안쪽으로 들어오면 바깥으로 밀어낸다.
        private static void Place(string item, Transform parent, Vector3 pos, float yaw, float maxFootprint, float maxHeight, bool alignAlongZ, float side)
        {
            float extraScale = 1f;
            int star = item.IndexOf('*');
            if (star >= 0)
            {
                extraScale = float.Parse(item.Substring(star + 1), System.Globalization.CultureInfo.InvariantCulture);
                item = item.Substring(0, star);
            }

            GameObject prefab = LoadSynty(item);
            if (prefab == null)
                return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale *= extraScale;

            Bounds b = RendererBounds(go);
            if (b.size == Vector3.zero)
            {
                Object.DestroyImmediate(go);
                return;
            }

            // 울타리·담장은 긴 쪽이 길 방향(z)을 따라가게 돌린다.
            if (alignAlongZ && b.size.x > b.size.z)
            {
                go.transform.localRotation = Quaternion.Euler(0f, yaw + 90f, 0f);
                b = RendererBounds(go);
            }

            float scale = 1f;
            float footprint = Mathf.Max(b.size.x, b.size.z);
            if (maxFootprint > 0f && footprint > maxFootprint)
                scale = maxFootprint / footprint;
            if (maxHeight > 0f && b.size.y * scale > maxHeight)
                scale = maxHeight / b.size.y;
            if (scale < 1f)
                go.transform.localScale *= scale;

            StripGameplayComponents(go);

            b = RendererBounds(go);
            Vector3 world = parent.TransformPoint(pos);
            go.transform.position += new Vector3(world.x - b.center.x, world.y - b.min.y, world.z - b.center.z);

            // 길(|x| < 5) 안쪽으로 삐져나오면 바깥으로 밀어낸다.
            b = RendererBounds(go);
            float limit = PathHalfWidth + PathClearance;
            if (side > 0f && b.min.x < limit)
                go.transform.position += new Vector3(limit - b.min.x, 0f, 0f);
            else if (side < 0f && b.max.x > -limit)
                go.transform.position += new Vector3(-limit - b.max.x, 0f, 0f);

            go.name = prefab.name;
        }

        // 장식이 러너·총알·적 판정에 끼어들지 않게 콜라이더와 리지드바디를 빼고, 모바일 부담을 줄이려고 라이트도 뺀다.
        private static void StripGameplayComponents(GameObject go)
        {
            foreach (Rigidbody body in go.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (Light light in go.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(light);
        }

        private static void BuildSideGround(Transform parent, float length, Material material)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.name = side < 0f ? "Side Ground L" : "Side Ground R";
                ground.transform.SetParent(parent, false);
                // 윗면이 길 바닥(y=0)보다 살짝 낮아 겹쳐 깜빡이지 않게 한다.
                ground.transform.localPosition = new Vector3(side * (PathHalfWidth + SideGroundWidth / 2f), -0.52f, 0f);
                ground.transform.localScale = new Vector3(SideGroundWidth, 1f, length);
                var meshRenderer = ground.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void SetPathMaterial(GameObject chunk, Material material)
        {
            Transform ground = chunk.transform.Find("Chunk Renderer");
            if (ground != null && ground.TryGetComponent(out MeshRenderer meshRenderer))
                meshRenderer.sharedMaterial = material;
            else
                Log($"{chunk.name}: Chunk Renderer 를 못 찾아 바닥색을 못 바꿨습니다");
        }

        private static float FinishLineZ(GameObject chunk)
        {
            foreach (Transform child in chunk.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag("Finish"))
                    return child.position.z - chunk.transform.position.z;
            }
            return 0f;
        }

        private static Material ThemeMaterial(string name, Color color)
        {
            string path = MaterialDir + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 씬의 ChunkManager 에 테마를 연결한다. 저장 안 된 변경이 있는 씬이면 연결만 하고 저장은 사용자에게 맡긴다.
        private static void AssignToScene(List<MapTheme> themeAssets)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Log($"현재 씬이 {ScenePath} 가 아니라 ChunkManager 연결은 건너뜀 (인스펙터에서 Themes 에 직접 넣어 주세요)");
                return;
            }

            var manager = Object.FindFirstObjectByType<ChunkManager>();
            if (manager == null)
            {
                Log("씬에 ChunkManager 가 없어 연결을 건너뜀");
                return;
            }

            bool wasDirty = scene.isDirty;
            var serialized = new SerializedObject(manager);
            SerializedProperty themes = serialized.FindProperty("themes");
            themes.arraySize = themeAssets.Count;
            for (int i = 0; i < themeAssets.Count; i++)
                themes.GetArrayElementAtIndex(i).objectReferenceValue = themeAssets[i];
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);

            if (wasDirty == false)
            {
                EditorSceneManager.SaveScene(scene);
                Log($"ChunkManager 에 테마 {themeAssets.Count}개 연결하고 씬 저장");
            }
            else
            {
                Log($"ChunkManager 에 테마 {themeAssets.Count}개 연결 (씬에 저장 안 된 변경이 있어 저장은 하지 않음)");
            }
        }

        // ───────────────────────── 미리보기 ─────────────────────────

        // 테마마다 실제 게임 카메라 구도(세로 화면)로 4장 + 전체 조감도 1장을 한 이미지로 찍는다.
        public static List<string> CapturePreviews(string dir)
        {
            Report.Clear();
            Directory.CreateDirectory(dir);
            var tempLight = EnsurePreviewLight();
            try
            {
                foreach (Theme theme in Themes)
                    CaptureTheme(theme, Path.Combine(dir, $"Theme_{theme.Id.Replace(' ', '_')}.png"));
            }
            finally
            {
                if (tempLight != null)
                    Object.DestroyImmediate(tempLight);
            }
            return new List<string>(Report);
        }

        private static void CaptureTheme(Theme theme, string pngPath)
        {
            string dir = OutDir + theme.Id + "/";
            string[] order = { "First Chunk", "Medium Chunk", "Long Chunk", "Long Chunk Coins", "Finish Chunk" };
            var holder = new GameObject("ThemePreview");
            Vector3 origin = new Vector3(3000f, 0f, 0f);
            var savedLighting = LightingSnapshot.Take();
            try
            {
                // 테마 조명이 있으면 실제 게임처럼 적용해서 찍는다 (찍은 뒤 원래대로 되돌림).
                var mapTheme = AssetDatabase.LoadAssetAtPath<MapTheme>($"{dir}{theme.Id} Theme.asset");
                previewBackground = null;
                if (mapTheme != null && mapTheme.Lighting != null && mapTheme.Lighting.enabled)
                {
                    mapTheme.ApplyLighting(savedLighting.Sun, null);
                    if (mapTheme.Lighting.useSolidBackground)
                        previewBackground = mapTheme.Lighting.backgroundColor;
                }

                float z = 0f;
                for (int i = 0; i < order.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}{theme.Id} {order[i]}.prefab");
                    if (prefab == null)
                    {
                        Log("MISSING " + theme.Id + " " + order[i]);
                        continue;
                    }
                    float length = prefab.GetComponent<Chunk>().GetLength();
                    if (i > 0)
                        z += length / 2f;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                    go.transform.position = origin + new Vector3(0f, 0f, z);
                    z += length / 2f;
                }

                const int w = 360, h = 780;
                float[] playerZ = { 0f, 30f, 60f, z - 30f };
                const int overviewH = 620;
                int sheetW = w * playerZ.Length;
                var sheet = new Texture2D(sheetW, h + overviewH, TextureFormat.RGB24, false);

                for (int i = 0; i < playerZ.Length; i++)
                {
                    // 씬 카메라: 플레이어 뒤 15m, 높이 15.7m, 35.7° 내려다봄, 세로 FOV 45
                    Vector3 cam = origin + new Vector3(0f, 15.7f, playerZ[i] - 15f);
                    var tex = Render(cam, new Vector3(35.7f, 0f, 0f), 45f, w, h);
                    sheet.SetPixels(i * w, overviewH, w, h, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                }

                var overview = Render(origin + new Vector3(-55f, 45f, z * 0.5f - 45f), new Vector3(35f, 50f, 0f), 50f, sheetW, overviewH);
                sheet.SetPixels(0, 0, sheetW, overviewH, overview.GetPixels());
                Object.DestroyImmediate(overview);

                sheet.Apply();
                File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                Log($"미리보기 {theme.Id} → {pngPath}");
            }
            finally
            {
                previewBackground = null;
                savedLighting.Restore();
                Object.DestroyImmediate(holder);
            }
        }

        private static Color? previewBackground;

        // 미리보기 때 바꾼 씬 조명을 원래대로 되돌리기 위한 스냅샷
        private class LightingSnapshot
        {
            public Light Sun;
            private Color sunColor;
            private float sunIntensity;
            private UnityEngine.Rendering.AmbientMode ambientMode;
            private Color ambientLight;
            private bool fog;
            private FogMode fogMode;
            private Color fogColor;
            private float fogStart;
            private float fogEnd;

            public static LightingSnapshot Take()
            {
                var snapshot = new LightingSnapshot
                {
                    Sun = RenderSettings.sun,
                    ambientMode = RenderSettings.ambientMode,
                    ambientLight = RenderSettings.ambientLight,
                    fog = RenderSettings.fog,
                    fogMode = RenderSettings.fogMode,
                    fogColor = RenderSettings.fogColor,
                    fogStart = RenderSettings.fogStartDistance,
                    fogEnd = RenderSettings.fogEndDistance,
                };

                if (snapshot.Sun == null)
                {
                    foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light.type == LightType.Directional)
                        {
                            snapshot.Sun = light;
                            break;
                        }
                    }
                }

                if (snapshot.Sun != null)
                {
                    snapshot.sunColor = snapshot.Sun.color;
                    snapshot.sunIntensity = snapshot.Sun.intensity;
                }
                return snapshot;
            }

            public void Restore()
            {
                if (Sun != null)
                {
                    Sun.color = sunColor;
                    Sun.intensity = sunIntensity;
                }
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
            }
        }

        private static GameObject EnsurePreviewLight()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.isActiveAndEnabled)
                    return null;
            }

            var go = new GameObject("PreviewSun");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return go;
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
                cam.farClipPlane = 400f;
                cam.nearClipPlane = 0.1f;
                cam.clearFlags = CameraClearFlags.Skybox;
                if (previewBackground.HasValue)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = previewBackground.Value;
                }
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                cam.Render(); // 첫 렌더는 셰이더 준비 전이라 색이 틀어질 때가 있다
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

        private static readonly Dictionary<string, string> Packs = new Dictionary<string, string>
        {
            { "FK", "Assets/Synty/PolygonFantasyKingdom" },
            { "VK", "Assets/Synty/PolygonVikings" },
            { "GN", "Assets/Synty/PolygonGeneric" },
            { "HC", "Assets/Synty/PolygonHorrorCarnival" },
            { "ER", "Assets/Synty/PolygonElvenRealm" },
            { "NT", "Assets/Synty/PolygonNature" },
            { "EF", "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest" },
        };

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        // "FK:SM_Prop_Fence_01" → 해당 팩에서 이름이 같은 프리팹
        private static GameObject LoadSynty(string item)
        {
            if (Cache.TryGetValue(item, out GameObject cached))
                return cached;

            GameObject found = null;
            string[] parts = item.Split(':');
            if (parts.Length == 2 && Packs.TryGetValue(parts[0], out string folder))
            {
                foreach (string guid in AssetDatabase.FindAssets(parts[1] + " t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == parts[1])
                    {
                        found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        break;
                    }
                }
            }

            if (found == null)
                Log("MISSING " + item);
            Cache[item] = found;
            return found;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            bool hasBounds = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer targetRenderer in go.GetComponentsInChildren<Renderer>())
            {
                // 불꽃 같은 파티클은 크기 재기에서 뺀다.
                if (targetRenderer is ParticleSystemRenderer)
                    continue;

                if (hasBounds)
                {
                    b.Encapsulate(targetRenderer.bounds);
                }
                else
                {
                    b = targetRenderer.bounds;
                    hasBounds = true;
                }
            }
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        private static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static void Log(string message) => Report.Add(message);
    }

    // 에디터가 열려 있을 때 외부에서 테마 청크 빌드를 요청하는 통로 (RunnerPrefabRequestWatcher 와 같은 방식)
    [InitializeOnLoad]
    public static class ChunkThemeRequestWatcher
    {
        private const string RequestDir = "Temp/ChunkThemeRequests";
        private static double nextPoll;

        static ChunkThemeRequestWatcher()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll)
                return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || Directory.Exists(RequestDir) == false)
                return;

            string[] requests = Directory.GetFiles(RequestDir, "*.request");
            if (requests.Length == 0)
                return;

            // 에디터가 백그라운드면 바뀐 스크립트를 아직 안 가져왔을 수 있다. 먼저 가져오고,
            // 컴파일이 시작되면 요청은 그대로 두고 도메인 리로드 뒤 새 코드로 처리한다.
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            foreach (string requestPath in requests)
            {
                string name = Path.GetFileNameWithoutExtension(requestPath);
                File.Delete(requestPath);

                var lines = new List<string>();
                void Capture(string message, string stack, LogType type)
                {
                    if (type == LogType.Exception || type == LogType.Error)
                        lines.Add($"{type}: {message}");
                }

                Application.logMessageReceived += Capture;
                bool ok = false;
                try
                {
                    if (name == "Flash")
                    {
                        lines.AddRange(EnemyThemeBuilder.CaptureFlashPreview(Path.Combine(RequestDir, "Flash.png")));
                    }
                    else if (name == "VerifyEnemies")
                    {
                        lines.AddRange(EnemyThemeBuilder.VerifyGround());
                    }
                    else if (name == "Enemies")
                    {
                        lines.AddRange(EnemyThemeBuilder.Build());
                        lines.AddRange(EnemyThemeBuilder.VerifyGround());
                        lines.AddRange(EnemyThemeBuilder.CapturePreview(Path.Combine(RequestDir, "Enemies.png")));
                    }
                    else
                    {
                        if (name == "Build")
                            lines.AddRange(ChunkThemeBuilder.Build());
                        lines.AddRange(ChunkThemeBuilder.CapturePreviews(RequestDir));
                    }
                    ok = true;
                }
                catch (System.Exception e)
                {
                    lines.Add($"Exception: {e}");
                }
                finally
                {
                    Application.logMessageReceived -= Capture;
                }

                lines.Add(ok ? "RESULT OK" : "RESULT FAIL");
                File.WriteAllLines(Path.Combine(RequestDir, name + ".result"), lines);
            }
        }
    }
}
