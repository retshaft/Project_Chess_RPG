using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.StatModifiers
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Stat Modifier Profile")]
    public class StatModifierProfileSO : ScriptableObject
    {
        [Tooltip("EffectSystem의 EffectId와 매칭되는 키")]
        public string EffectId;

        [Tooltip("이 이펙트가 적용할 스탯 수정자 목록")]
        public List<StatModifierEntry> Modifiers = new();
    }
}
