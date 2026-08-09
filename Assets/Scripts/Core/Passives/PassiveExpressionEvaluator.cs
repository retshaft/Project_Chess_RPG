using System;
using NCalc;
using CheckmateRPG.Components;

namespace CheckmateRPG.Core.Passives
{
    public static class PassiveExpressionEvaluator
    {
        public static bool Evaluate(string expression, Guid actorId, Guid targetId, int currentHitCount)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            // 1. 수식 객체 생성
            Expression e = new Expression(expression);

            // 2. 수식 안의 변수(파라미터)들에 실제 게임 데이터를 채워넣음
            e.Parameters["TargetIsBleeding"] = ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var targetBrain) && targetBrain.GetComponent<StatusEffectComponent>().HasStatus(StatusEffectType.Bleed);
            e.Parameters["HitCount"] = currentHitCount;

            // 3. 수식 계산 및 true/false 반환
            var result = e.Evaluate();
            if (result is bool boolResult)
                return boolResult;
            
            return false;
        }
    }
}