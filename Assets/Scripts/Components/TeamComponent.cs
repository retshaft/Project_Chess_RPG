using UnityEngine;

namespace CheckmateRPG.Components
{
    public class TeamComponent : MonoBehaviour
    {
        [Tooltip("True if this unit belongs to the enemy team.")]
        [SerializeField] private bool _isEnemy;

        public bool IsEnemy => _isEnemy;
        public bool IsPlayer => !_isEnemy;

        public void SetIsEnemy(bool isEnemy)
        {
            _isEnemy = isEnemy;
        }
    }
}
