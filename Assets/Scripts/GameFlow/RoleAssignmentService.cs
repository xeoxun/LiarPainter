using System;
using System.Collections.Generic;

public static class RoleAssignmentService
{
    public static IReadOnlyList<LiarPainterRole> CreateRoles(int playerCount, Random random)
    {
        if (playerCount < 4)
            throw new ArgumentOutOfRangeException(nameof(playerCount), "게임을 시작하려면 최소 4명이 필요합니다.");
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        List<LiarPainterRole> roles = new(playerCount)
        {
            LiarPainterRole.FakePainter,
            LiarPainterRole.Appraiser
        };

        // 4명은 사짜 화가 1명, 감정가 1명, 시민 2명으로 고정합니다.
        // 5명부터는 두 고정 직업에 알코올 중독 화가를 0~2명 무작위로 추가합니다.
        int drunkPainterCount = playerCount >= 5 ? random.Next(0, 3) : 0;
        for (int i = 0; i < drunkPainterCount; i++)
            roles.Add(LiarPainterRole.DrunkPainter);

        while (roles.Count < playerCount) roles.Add(LiarPainterRole.Citizen);
        Shuffle(roles, random);
        return roles;
    }

    public static void Shuffle<T>(IList<T> values, Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int target = random.Next(i + 1);
            (values[i], values[target]) = (values[target], values[i]);
        }
    }
}
