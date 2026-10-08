using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LiarPainterKeywordCategory
{
    Animal,
    SeaCreature,
    Food,
    DessertAndFruit,
    Household,
    Electronics,
    Vehicle,
    Nature,
    Fantasy,
    PlayAndSports
}

[Serializable]
public sealed class LiarPainterKeywordEntry
{
    public string keyword;
    public LiarPainterKeywordCategory category;

    public LiarPainterKeywordEntry(string value, LiarPainterKeywordCategory keywordCategory)
    {
        keyword = value;
        category = keywordCategory;
    }
}

[CreateAssetMenu(fileName = "LiarPainterContentCatalog", menuName = "Liar Painter/Content Catalog")]
public sealed class LiarPainterContentCatalog : ScriptableObject
{
    [SerializeField] private List<string> questions = new();
    [SerializeField] private List<LiarPainterKeywordEntry> keywords = new();

    public IReadOnlyList<string> Questions => questions;
    public IReadOnlyList<LiarPainterKeywordEntry> Keywords => keywords;

    public void ReplaceContent(IEnumerable<string> newQuestions, IEnumerable<LiarPainterKeywordEntry> newKeywords)
    {
        questions = newQuestions?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList() ?? new List<string>();
        keywords = newKeywords?.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.keyword)).ToList()
                   ?? new List<LiarPainterKeywordEntry>();
    }

    public bool TryCreateRound(System.Random random, out LiarPainterRoundContent content)
    {
        content = null;
        if (random == null) throw new ArgumentNullException(nameof(random));

        string[] validQuestions = questions.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        LiarPainterKeywordEntry[] validKeywords = keywords
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.keyword))
            .ToArray();
        LiarPainterKeywordEntry[] selectableCitizenKeywords = validKeywords
            .Where(entry => validKeywords.Count(other => other.category == entry.category && other.keyword != entry.keyword) > 0)
            .ToArray();

        if (validQuestions.Length == 0 || selectableCitizenKeywords.Length == 0) return false;

        LiarPainterKeywordEntry citizen = selectableCitizenKeywords[random.Next(selectableCitizenKeywords.Length)];
        LiarPainterKeywordEntry[] drunkCandidates = validKeywords
            .Where(entry => entry.category == citizen.category && entry.keyword != citizen.keyword)
            .ToArray();
        LiarPainterKeywordEntry drunk = drunkCandidates[random.Next(drunkCandidates.Length)];

        content = new LiarPainterRoundContent
        {
            citizenKeyword = citizen.keyword,
            drunkPainterKeyword = drunk.keyword,
            question = validQuestions[random.Next(validQuestions.Length)]
        };
        return true;
    }
}

public static class LiarPainterContentProvider
{
    private const string ResourceName = "LiarPainterContentCatalog";

    public static LiarPainterContentCatalog LoadDefault()
    {
        return Resources.Load<LiarPainterContentCatalog>(ResourceName);
    }
}
