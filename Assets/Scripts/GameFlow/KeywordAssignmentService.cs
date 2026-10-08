public static class KeywordAssignmentService
{
    public static string GetPrivateKeyword(LiarPainterRole role, LiarPainterRoundContent content)
    {
        return role switch
        {
            LiarPainterRole.FakePainter => string.Empty,
            LiarPainterRole.DrunkPainter => content.drunkPainterKeyword,
            _ => content.citizenKeyword
        };
    }

    public static LiarPainterRole GetVisibleRole(LiarPainterRole actualRole)
    {
        // 알코올 중독 화가는 본인이 특수 직업이라는 사실을 모릅니다.
        return actualRole == LiarPainterRole.DrunkPainter
            ? LiarPainterRole.Citizen
            : actualRole;
    }
}
