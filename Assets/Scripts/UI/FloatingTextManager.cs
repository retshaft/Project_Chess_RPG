using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.UI
{
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        private List<FloatingText> _pool = new List<FloatingText>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SpawnText(Vector3 position, string text, Color color, float duration = 0.8f, float maxScale = 1.5f)
        {
            FloatingText floatingText = GetFromPool();
            floatingText.transform.position = position;
            floatingText.Initialize(text, color, duration, maxScale);
        }

        public void SpawnDamage(Vector3 position, float amount, bool isCrit, bool isBleeding = false)
        {
            string text = isBleeding ? $"[출혈] {amount:F0}" : (isCrit ? $"<b>{amount:F0} !</b>" : $"{amount:F0}");
            Color color = isBleeding ? new Color(0.95f, 0.15f, 0.25f) : (isCrit ? new Color(1f, 0.6f, 0f) : Color.white);
            float duration = isCrit || isBleeding ? 1.0f : 0.7f;
            Vector3 jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.1f, 0.2f), 0f);
            SpawnText(position + jitter, text, color, duration, isCrit ? 2.2f : 1.4f);
        }

        public void SpawnStatusText(Vector3 position, string statusName, Color color)
        {
            Vector3 offset = new Vector3(0f, 0.5f, 0f);
            SpawnText(position + offset, $"[{statusName}]", color, 0.9f, 1.6f);
        }

        public void SpawnHeal(Vector3 position, float amount)
        {
            SpawnText(position, $"+{amount:F0}", new Color(0.2f, 0.95f, 0.4f), 0.85f, 1.5f);
        }

        public void SpawnSnipeDamage(Vector3 position, float amount, bool isSpecial = false)
        {
            string text = isSpecial ? $"🎯 <b>[아스트럴 저격] {amount:F0}!</b>" : $"🎯 <b>[저격] {amount:F0}</b>";
            Color color = new Color(0.1f, 0.95f, 0.85f); // 에메랄드 시안
            Vector3 jitter = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.2f, 0.5f), 0f);
            SpawnText(position + jitter, text, color, isSpecial ? 1.3f : 1.0f, isSpecial ? 2.8f : 2.2f);
        }

        public void SpawnSiegeDamage(Vector3 position, float amount, bool isArmorBreak = false)
        {
            string text = isArmorBreak ? $"💥 <b>[궤도 파성 포격] {amount:F0}!</b>" : $"💥 <b>[대장갑 공성] {amount:F0}</b>";
            Color color = new Color(1f, 0.45f, 0.1f); // 다크 오렌지 / 융합 에너지
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.4f), 0f);
            SpawnText(position + jitter, text, color, isArmorBreak ? 1.4f : 1.0f, isArmorBreak ? 3.0f : 2.3f);
        }

        private FloatingText GetFromPool()
        {
            foreach (var item in _pool)
            {
                if (!item.gameObject.activeInHierarchy)
                {
                    item.gameObject.SetActive(true);
                    return item;
                }
            }

            GameObject go = new GameObject("FloatingText_Pooled");
            go.transform.SetParent(transform);
            FloatingText ft = go.AddComponent<FloatingText>();
            _pool.Add(ft);
            return ft;
        }
    }
}
