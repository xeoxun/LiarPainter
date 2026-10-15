using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameRulesPopupController : MonoBehaviour
{
    [Serializable]
    private sealed class RulePage
    {
        public string title;
        [TextArea(5, 16)] public string body;
        public RulePage(string title, string body) { this.title = title; this.body = body; }
    }

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RulePage[] pages =
    {
        new RulePage("게임 소개", "질문과 제시어를 연결해 그림을 그리고, 제시어를 모르는 ‘사짜 화가’를 찾아내세요!\n\n참여 인원: 4~8명 / 한 게임: 총 2라운드\n\n4명: 사짜 화가 1명, 감정가 1명, 시민 2명\n5명 이상: 사짜 화가 1명, 감정가 1명 고정\n알코올 중독 화가 0~1명 무작위 배정, 나머지는 시민\n\n모든 플레이어가 준비하면 호스트가 게임을 시작합니다."),
        new RulePage("시민", "공통 제시어를 알고 시작하는 시민 팀입니다.\n\n질문과 자신의 제시어를 연결해 그림을 그리세요.\n다른 사람의 그림과 설명을 살펴보고 사짜 화가를 추리하세요.\n\n승리 조건\n사짜 화가가 제시어를 맞히지 못한 상태에서,\n최종 투표로 사짜 화가를 지목하면 시민 팀이 승리합니다."),
        new RulePage("감정가", "시민과 같은 제시어를 받는 시민 팀입니다.\n\n검증 시간에 원하는 선에 마우스를 올리고 클릭하면\n그 선을 그린 플레이어를 확인할 수 있습니다.\n\n능력은 라운드마다 1회 사용할 수 있으며,\n2라운드가 시작되면 다시 1회 사용할 수 있습니다.\n\n승리 조건은 시민과 같습니다."),
        new RulePage("사짜 화가", "제시어를 받지 못하는 역할입니다.\n\n질문과 다른 사람의 그림을 보며 제시어를 유추하고,\n정체를 숨기면서 자연스럽게 그림을 그리세요.\n\n각 라운드의 검증 시간에 제시어 맞히기 팝업이 열립니다.\n시간 안에는 횟수 제한 없이 도전할 수 있습니다.\n정답을 맞히면 즉시 사짜 화가의 승리로 끝납니다!\n\n정답을 못 맞히고 최종 투표로 지목되면 패배,\n다른 사람이 지목되면 전원 무승부입니다."),
        new RulePage("알코올 중독 화가", "자신도 모르게 다른 제시어를 받은 시민 팀입니다.\n\n일반 시민과 다른 제시어를 받지만,\n본인에게는 직업이 ‘시민’으로 안내됩니다.\n자신이 알코올 중독 화가인지 알 수 없습니다.\n\n자신에게 주어진 제시어를 기준으로 그림을 그리세요.\n다른 사람과 그림의 방향이 달라도 사짜가 아닐 수 있습니다!\n\n승리 조건은 시민과 같습니다."),
        new RulePage("게임 진행 순서", "① 직업·제시어 확인 — 5초\n자신의 직업과 제시어를 확인합니다.\n\n② 질문 공개 — 5초\n질문은 공개 이후에도 화면에 표시됩니다.\n\n③ 그림 그리기 — 한 명당 15초\n무작위 순서로 한 명씩 그림을 그립니다.\n\n④ 검증 — 20초\n그림을 보며 의견을 나누고 직업 능력을 사용합니다.\n\n②~④를 한 번 더 진행한 뒤 최종 투표를 시작합니다."),
        new RulePage("그림과 라운드 규칙", "그림은 모든 플레이어에게 실시간으로 보이지만,\n현재 누가 그리는지는 공개되지 않습니다.\n\n자신의 턴에는 캔버스 테두리와 내 슬롯이 빛납니다.\n펜 색상을 선택하고, 되돌리기로 최근 자신의 선을 지울 수 있습니다.\n\n그림을 그리지 않으면 슬롯에 ‘3번째 - 그림 미작성’처럼\n미작성자의 신원과 턴 순서가 모두에게 공개됩니다.\n\n2라운드에는 그림과 미작성 기록이 초기화되고,\n질문과 그리는 순서가 바뀝니다. 직업과 제시어는 유지됩니다."),
        new RulePage("투표와 최후 변론", "2라운드 검증이 끝나면 10초 동안 투표합니다.\n\n자신에게도 투표할 수 있으며, 시간 내 선택을 변경할 수 있습니다.\n투표하지 않으면 기권으로 처리됩니다.\n\n최다 득표자가 동점이면 동점자만 대상으로\n10초간 재투표합니다. 다시 동점이면 재투표를 반복합니다.\n\n한 명이 선정되면 15초간 최후 변론을 진행하며,\n선정된 플레이어만 그림을 그릴 수 있습니다.\n추가 투표 없이 결과가 공개됩니다."),
        new RulePage("승리와 무승부", "사짜가 검증 시간에 시민의 제시어를 맞힘\n→ 사짜 화가 승리 / 시민 팀 패배\n\n사짜가 정답을 못 맞히고, 최종 투표로 지목됨\n→ 시민 팀 승리 / 사짜 화가 패배\n\n사짜가 정답을 못 맞히고, 다른 사람이 최종 선정됨\n→ 전원 무승부\n\n최종 투표에서 전원 기권\n→ 전원 무승부\n\n결과 창에는 모든 직업과 받은 제시어가 공개됩니다.\n대기실로 돌아온 뒤 결과 창은 각자 닫을 수 있습니다.")
    };
    private int pageIndex;

    private void OnEnable() => RefreshPage();
    public void Open()
    {
        pageIndex = 0;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        RefreshPage();
    }
    public void Close() => gameObject.SetActive(false);
    public void Previous() { pageIndex--; RefreshPage(); }
    public void Next() { pageIndex++; RefreshPage(); }

    private void RefreshPage()
    {
        if (pages == null || pages.Length == 0) return;
        pageIndex = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
        titleText.text = pages[pageIndex].title;
        bodyText.text = FormatRuleTimes(pages[pageIndex].title, pages[pageIndex].body);
        pageText.text = $"{pageIndex + 1} / {pages.Length}";
        previousButton.interactable = true;
        nextButton.interactable = true;
        previousButton.gameObject.SetActive(pageIndex > 0);
        nextButton.gameObject.SetActive(pageIndex < pages.Length - 1);
    }

    private static string FormatRuleTimes(string title, string text)
    {
        // Preserve authored wording while sourcing displayed timings from the shared rules.
        if (title == "게임 진행 순서")
        {
            text = ReplaceTime(text, @"(직업·제시어 확인\s*[—–-]\s*)\d+(?:\.\d+)?초", LiarPainterRuleSettings.roleRevealSeconds);
            text = ReplaceTime(text, @"(질문 공개\s*[—–-]\s*)\d+(?:\.\d+)?초", LiarPainterRuleSettings.questionRevealSeconds);
            text = ReplaceTime(text, @"(그림 그리기\s*[—–-]\s*한 명당\s*)\d+(?:\.\d+)?초", LiarPainterRuleSettings.drawingSecondsPerPlayer);
            text = ReplaceTime(text, @"(검증\s*[—–-]\s*)\d+(?:\.\d+)?초", LiarPainterRuleSettings.discussionSeconds);
        }
        else if (title == "투표와 최후 변론")
        {
            text = ReplaceTime(text, @"()\d+(?:\.\d+)?초(?= 동안 투표|간 재투표)", LiarPainterRuleSettings.votingSeconds);
            text = ReplaceTime(text, @"()\d+(?:\.\d+)?초(?=간 최후 변론)", LiarPainterRuleSettings.finalDefenseSeconds);
        }
        return text;
    }

    private static string ReplaceTime(string text, string pattern, float seconds) =>
        System.Text.RegularExpressions.Regex.Replace(text, pattern,
            match => match.Groups[1].Value + seconds.ToString("0.##") + "초");
}
