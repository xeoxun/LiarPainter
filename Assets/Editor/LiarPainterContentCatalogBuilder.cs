using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LiarPainterContentCatalogBuilder
{
    private const string AssetPath = "Assets/Resources/LiarPainterContentCatalog.asset";

    static LiarPainterContentCatalogBuilder()
    {
        EditorApplication.delayCall += EnsureCatalog;
    }

    [MenuItem("Liar Painter/Content/Rebuild Default Catalog")]
    public static void RebuildCatalog()
    {
        CreateOrUpdate(true);
    }

    private static void EnsureCatalog()
    {
        CreateOrUpdate(false);
    }

    private static void CreateOrUpdate(bool overwrite)
    {
        LiarPainterContentCatalog catalog = AssetDatabase.LoadAssetAtPath<LiarPainterContentCatalog>(AssetPath);
        if (catalog != null && !overwrite && catalog.Questions.Count > 0 && catalog.Keywords.Count > 0) return;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LiarPainterContentCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        catalog.ReplaceContent(CreateQuestions(), CreateKeywords());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Liar Painter] 질문 {catalog.Questions.Count}개와 키워드 {catalog.Keywords.Count}개를 저장했습니다: {AssetPath}");
    }

    private static IEnumerable<string> CreateQuestions()
    {
        return new[]
        {
            "이것이 가장 잘 어울리는 장소는?",
            "이것과 함께 있으면 좋은 것은?",
            "이것을 처음 발견한 사람의 반응은?",
            "이것이 아주 거대해진다면 어떤 일이 벌어질까?",
            "이것이 아주 작아진다면 어디에 숨어 있을까?",
            "이것이 살아 움직인다면 무엇을 할까?",
            "이것이 사람을 도와준다면 어떻게 도와줄까?",
            "이것이 위험해진다면 어떤 모습일까?",
            "이것이 주인공이라면 어떤 장면이 나올까?",
            "이것이 악당이라면 어떤 일을 벌일까?",
            "이것을 선물로 받는다면 어떤 상황일까?",
            "이것이 학교에 나타난다면 어떤 일이 생길까?",
            "이것이 무인도에 있다면 어떻게 활용할까?",
            "이것이 미래에 존재한다면 어떤 모습일까?",
            "이것이 아주 오래전에 존재했다면 어떤 모습일까?",
            "이것을 밤에 만난다면 어떤 장면일까?",
            "이것이 비 오는 날에는 어떤 모습일까?",
            "이것이 겨울을 보낸다면 어떤 모습일까?",
            "이것이 꿈속에 나타난다면 어떤 모습일까?",
            "이것 때문에 큰 소동이 벌어진다면 어떤 장면일까?"
        };
    }

    private static IEnumerable<LiarPainterKeywordEntry> CreateKeywords()
    {
        List<LiarPainterKeywordEntry> entries = new(100);
        Add(entries, LiarPainterKeywordCategory.Animal,
            "고양이", "강아지", "토끼", "호랑이", "사자", "코끼리", "기린", "원숭이", "펭귄", "악어");
        Add(entries, LiarPainterKeywordCategory.SeaCreature,
            "상어", "문어", "고래", "돌고래", "해파리", "거북이", "오징어", "불가사리", "꽃게", "복어");
        Add(entries, LiarPainterKeywordCategory.Food,
            "피자", "햄버거", "김밥", "라면", "초밥", "떡볶이", "치킨", "샌드위치", "만두", "핫도그");
        Add(entries, LiarPainterKeywordCategory.DessertAndFruit,
            "케이크", "도넛", "아이스크림", "초콜릿", "사탕", "수박", "바나나", "딸기", "사과", "파인애플");
        Add(entries, LiarPainterKeywordCategory.Household,
            "우산", "가방", "안경", "시계", "의자", "침대", "거울", "칫솔", "열쇠", "휴지");
        Add(entries, LiarPainterKeywordCategory.Electronics,
            "컴퓨터", "스마트폰", "텔레비전", "카메라", "이어폰", "냉장고", "선풍기", "청소기", "게임기", "드론");
        Add(entries, LiarPainterKeywordCategory.Vehicle,
            "자동차", "버스", "기차", "비행기", "자전거", "오토바이", "배", "잠수함", "로켓", "열기구");
        Add(entries, LiarPainterKeywordCategory.Nature,
            "해", "달", "별", "구름", "번개", "무지개", "나무", "꽃", "화산", "폭포");
        Add(entries, LiarPainterKeywordCategory.Fantasy,
            "용", "유령", "좀비", "마녀", "마법사", "요정", "인어", "외계인", "로봇", "괴물");
        Add(entries, LiarPainterKeywordCategory.PlayAndSports,
            "축구공", "농구공", "야구방망이", "스케이트보드", "낚싯대", "텐트", "기타", "풍선", "장난감 총", "보물상자");
        return entries;
    }

    private static void Add(List<LiarPainterKeywordEntry> target, LiarPainterKeywordCategory category, params string[] values)
    {
        foreach (string value in values)
            target.Add(new LiarPainterKeywordEntry(value, category));
    }
}
