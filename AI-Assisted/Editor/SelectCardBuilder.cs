using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Animal_Run.Scripts.EditorTools
{
    // 열린 씬의 SelectUI 카드(동물 · 무기 · 증강 각 3장)를 선택 카드 UI 사양대로 다시 배치한다.
    // - 사양: Docs/UI_디자인_프롬프트_선택카드.md (1080 × 1920 기준)
    // - 기존 그림 · 이름 · 설명 오브젝트는 그대로 쓰고 위치 · 크기 · 글자만 바꾼다. 테두리 안쪽 · 그림 칸 · 배지 · 태그는 새로 만든다.
    // - 여러 번 실행해도 같은 결과가 나오도록, 새로 만드는 오브젝트는 이름으로 찾아 재사용한다.
    // - 글자는 레거시 Text. 스프라이트는 임시로 Sprites/UI 의 둥근 사각형 · 원을 쓰고, 장식 스프라이트가 생기면 바꿔 끼운다.
    // 메뉴: Tools/Animal Run/Build Select Cards  (실행 후 씬 저장은 직접)
    public static class SelectCardBuilder
    {
        private const string RoundedPath = "Assets/Animal Run/Sprites/UI/Rounded Container.png";
        private const string CirclePath = "Assets/Animal Run/Sprites/UI/Circle.png";

        private static readonly Vector2 CardSize = new Vector2(312, 650);
        private const float CardSpacing = 24f;
        private const float CardsY = -60f;
        private const float HeaderHeight = 330f;   // 카드 위쪽 색 영역(그림 칸이 들어감)

        private static Sprite rounded;
        private static Sprite circle;
        private static Font font;

        // 외부 요청: Temp/SelectCardRequests/Build.request → 같은 폴더에 Build.result (로그, 마지막 줄 RESULT OK / FAIL)
        private const string RequestDir = "Temp/SelectCardRequests";
        private static double nextPoll;

        [InitializeOnLoadMethod]
        private static void WatchRequests() => EditorApplication.update += PollRequests;

        private static void PollRequests()
        {
            if (EditorApplication.timeSinceStartup < nextPoll)
                return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || !Directory.Exists(RequestDir))
                return;

            foreach (string requestPath in Directory.GetFiles(RequestDir, "*.request"))
            {
                string name = Path.GetFileNameWithoutExtension(requestPath);
                File.Delete(requestPath);

                var lines = new List<string>();
                void Capture(string message, string stack, LogType type)
                {
                    if (message.StartsWith("[SelectCardBuilder]") || type == LogType.Exception || type == LogType.Error)
                        lines.Add($"{type}: {message}");
                }

                Application.logMessageReceived += Capture;
                bool ok;
                try
                {
                    ok = BuildFromMenu();
                    if (ok && name == "BuildAndSave")
                        ok = EditorSceneManager.SaveOpenScenes();
                    lines.Add("Scene: " + EditorSceneManager.GetActiveScene().path);
                }
                catch (System.Exception e)
                {
                    lines.Add("Exception: " + e);
                    ok = false;
                }
                finally
                {
                    Application.logMessageReceived -= Capture;
                }

                lines.Add(ok ? "RESULT OK" : "RESULT FAIL");
                File.WriteAllLines(Path.Combine(RequestDir, name + ".result"), lines);
            }
        }

        [MenuItem("Tools/Animal Run/Build Select Cards")]
        private static void BuildMenu() => BuildFromMenu();

        private static bool BuildFromMenu()
        {
            SelectUI selectUI = Object.FindFirstObjectByType<SelectUI>(FindObjectsInactive.Include);
            if (selectUI == null)
            {
                Debug.LogError("[SelectCardBuilder] 열린 씬에 SelectUI 가 없습니다. Animal Run 씬을 열고 다시 실행하세요.");
                return false;
            }

            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);

            var so = new SerializedObject(selectUI);
            Build(so, "animalSelectPanel", "animalButtons", "함께 달릴 동물을 고르세요",
                SelectCardStyle.AnimalFrame, SelectCardStyle.AnimalBadge, "중형");
            Build(so, "weaponSelectPanel", "weaponButtons", "무기를 고르세요",
                SelectCardStyle.WeaponFrame, SelectCardStyle.WeaponBadge, "총");
            Build(so, "augmentSelectPanel", "augmentButtons", "레벨 업! 강화를 고르세요",
                SelectCardStyle.AugmentFrame, SelectCardStyle.WeaponAugment, "무기 강화");
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(selectUI.gameObject.scene);
            Debug.Log("[SelectCardBuilder] 선택 카드 배치 완료. 씬을 저장하세요.");
            return true;
        }

        private static void Build(SerializedObject so, string panelField, string buttonsField, string title,
            Color frameColor, Color badgeColor, string previewTag)
        {
            var panel = so.FindProperty(panelField).objectReferenceValue as GameObject;
            SerializedProperty buttons = so.FindProperty(buttonsField);
            if (panel == null || buttons == null || buttons.arraySize == 0)
            {
                Debug.LogWarning($"[SelectCardBuilder] {panelField} 또는 {buttonsField} 가 비어 있어 건너뜁니다.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(panel, "Build Select Cards");

            // 패널: 화면 전체를 덮는 반투명 어두운 배경
            var panelRect = (RectTransform)panel.transform;
            Stretch(panelRect, Vector2.zero, Vector2.zero);
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = null;
                panelImage.color = SelectCardStyle.PanelDim;
            }

            font = FindFont(buttons);
            BuildTitle(panelRect, title);

            int count = buttons.arraySize;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = buttons.GetArrayElementAtIndex(i);
                float x = (i - (count - 1) * 0.5f) * (CardSize.x + CardSpacing);
                BuildCard(element, panelRect, new Vector2(x, CardsY), frameColor, badgeColor, previewTag);
            }
        }

        private static void BuildTitle(RectTransform panel, string title)
        {
            RectTransform ribbon = GetOrCreate(panel, "Card Title Ribbon");
            Place(ribbon, new Vector2(0, 520), new Vector2(880, 210));
            Image ribbonImage = GetOrAdd<Image>(ribbon.gameObject);
            SetSliced(ribbonImage, rounded, SelectCardStyle.TitleRibbon, 50f / 32f);

            RectTransform label = GetOrCreate(ribbon, "Card Title Text");
            Stretch(label, new Vector2(24, 12), new Vector2(-24, -12));
            Text text = GetOrAdd<Text>(label.gameObject);
            SetText(text, title, 56, Color.white, FontStyle.Bold);
            ribbon.SetAsLastSibling();
        }

        private static void BuildCard(SerializedProperty element, RectTransform panel, Vector2 position,
            Color frameColor, Color badgeColor, string previewTag)
        {
            var picture = element.FindPropertyRelative("buttonImage").objectReferenceValue as Image;
            var nameText = element.FindPropertyRelative("name").objectReferenceValue as Text;
            var descText = element.FindPropertyRelative("description").objectReferenceValue as Text;
            if (picture == null || nameText == null || descText == null)
            {
                Debug.LogWarning("[SelectCardBuilder] 그림 · 이름 · 설명 중 비어 있는 카드가 있어 건너뜁니다.");
                return;
            }

            Button button = picture.GetComponentInParent<Button>(true);
            var card = (RectTransform)(button != null ? button.transform : picture.transform.parent);
            if (card == picture.transform)
            {
                Debug.LogWarning($"[SelectCardBuilder] {card.name}: 그림이 버튼 자신에 붙어 있어 건너뜁니다. 그림을 자식 오브젝트로 옮겨 주세요.");
                return;
            }
            if (card.parent != panel)
                Debug.LogWarning($"[SelectCardBuilder] {card.name} 이 패널 바로 아래가 아닙니다. 위치가 어긋날 수 있습니다.");

            Place(card, position, CardSize);

            // 테두리(카드 바탕): 화면별 색. 살짝 떠 보이게 그림자를 단다.
            Image frame = GetOrAdd<Image>(card.gameObject);
            SetSliced(frame, rounded, frameColor, 50f / 32f);
            if (button != null && button.targetGraphic == null)
                button.targetGraphic = frame;
            Shadow shadow = GetOrAdd<Shadow>(card.gameObject);
            shadow.effectColor = new Color(0, 0, 0, 0.35f);
            shadow.effectDistance = new Vector2(0, -10);

            // 아래쪽 흰 영역(이름 · 효과 · 태그가 들어감)
            RectTransform inner = GetOrCreate(card, "Card Inner");
            Stretch(inner, new Vector2(8, 8), new Vector2(-8, -HeaderHeight));
            SetSliced(GetOrAdd<Image>(inner.gameObject), rounded, Color.white, 50f / 24f);
            NoRaycast(inner);

            // 그림 칸 (정사각형 1:1)
            RectTransform slot = GetOrCreate(card, "Picture Slot");
            Place(slot, new Vector2(0, 161), new Vector2(280, 280));
            SetSliced(GetOrAdd<Image>(slot.gameObject), rounded, SelectCardStyle.PictureSlot, 50f / 24f);
            NoRaycast(slot);

            var pictureRect = (RectTransform)picture.transform;
            pictureRect.SetParent(card, false);
            Place(pictureRect, new Vector2(0, 161), new Vector2(250, 250));
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            var nameRect = (RectTransform)nameText.transform;
            nameRect.SetParent(card, false);
            Place(nameRect, new Vector2(0, -45), new Vector2(296, 64));
            SetText(nameText, nameText.text, 48, SelectCardStyle.NameText, FontStyle.Bold);

            var descRect = (RectTransform)descText.transform;
            descRect.SetParent(card, false);
            Place(descRect, new Vector2(0, -165), new Vector2(296, 160));
            SetText(descText, descText.text, 34, SelectCardStyle.NameText, FontStyle.Normal);
            descText.lineSpacing = 0.9f;

            // 태그 (알약 모양)
            RectTransform tagBg = GetOrCreate(card, "Card Tag");
            Place(tagBg, new Vector2(0, -276), new Vector2(180, 58));
            Image tagImage = GetOrAdd<Image>(tagBg.gameObject);
            SetSliced(tagImage, rounded, frameColor, 50f / 20f);
            NoRaycast(tagBg);

            RectTransform tagLabel = GetOrCreate(tagBg, "Card Tag Text");
            Stretch(tagLabel, Vector2.zero, Vector2.zero);
            Text tagText = GetOrAdd<Text>(tagLabel.gameObject);
            SetText(tagText, previewTag, 30, Color.white, FontStyle.Bold);

            // 왼쪽 위 배지
            RectTransform badgeRect = GetOrCreate(card, "Card Badge");
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0, 1);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(22, -22);
            badgeRect.sizeDelta = new Vector2(72, 72);
            Image badge = GetOrAdd<Image>(badgeRect.gameObject);
            badge.sprite = circle;
            badge.type = Image.Type.Simple;
            badge.color = badgeColor;
            NoRaycast(badgeRect);
            Outline badgeOutline = GetOrAdd<Outline>(badgeRect.gameObject);
            badgeOutline.effectColor = Color.white;
            badgeOutline.effectDistance = new Vector2(4, -4);

            // 그리는 순서: 안쪽 → 그림 칸 → 그림 → 이름 → 효과 → 태그 → 배지
            inner.SetSiblingIndex(0);
            slot.SetSiblingIndex(1);
            pictureRect.SetSiblingIndex(2);
            nameRect.SetSiblingIndex(3);
            descRect.SetSiblingIndex(4);
            tagBg.SetSiblingIndex(5);
            badgeRect.SetAsLastSibling();

            element.FindPropertyRelative("frame").objectReferenceValue = frame;
            element.FindPropertyRelative("badge").objectReferenceValue = badge;
            element.FindPropertyRelative("tagBackground").objectReferenceValue = tagImage;
            element.FindPropertyRelative("tag").objectReferenceValue = tagText;
        }

        // ---------- 도우미 ----------

        private static Font FindFont(SerializedProperty buttons)
        {
            for (int i = 0; i < buttons.arraySize; i++)
            {
                var text = buttons.GetArrayElementAtIndex(i).FindPropertyRelative("name").objectReferenceValue as Text;
                if (text != null && text.font != null)
                    return text.font;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static RectTransform GetOrCreate(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return (RectTransform)found;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(go, "Build Select Cards");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // 둥근 사각형 스프라이트의 테두리(50px)를 원하는 모서리 둥글기로 줄인다.
        private static void SetSliced(Image image, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = color;
        }

        private static void SetText(Text text, string value, int size, Color color, FontStyle style)
        {
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        private static void NoRaycast(RectTransform rect)
        {
            var graphic = rect.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = false;
        }
    }
}
