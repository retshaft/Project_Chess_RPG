using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.UI
{
    public enum OutgameViewType
    {
        None,
        Title,
        MainLobby,
        StageSelect,
        DeckBuilder,
        Roster,
        SynchroBoard,
        Recruit,
        OperatorHub,
        Archive,
        KingSuitBay
    }

    public abstract class OutgameViewBase : MonoBehaviour
    {
        public OutgameViewType ViewType;
        private Coroutine _transitionRoutine;

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (gameObject.activeInHierarchy)
            {
                if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
                _transitionRoutine = StartCoroutine(AnimateShowTransition());
            }
        }

        public virtual void Hide()
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            gameObject.SetActive(false);
        }

        private IEnumerator AnimateShowTransition()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            float duration = 0.22f;
            float elapsed = 0f;

            canvasGroup.alpha = 0f;
            transform.localScale = new Vector3(0.95f, 0.95f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, easeOut);
                transform.localScale = Vector3.Lerp(new Vector3(0.95f, 0.95f, 1f), Vector3.one, easeOut);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 단일 씬 아웃게임 환경에서 Canvas 패널들을 전환(On/Off)해주는 중앙 라우터입니다.
    /// </summary>
    public class OutgameUIManager : MonoBehaviour
    {
        public static OutgameUIManager Instance { get; private set; }

        [SerializeField] private List<OutgameViewBase> views;
        
        private OutgameViewBase currentView;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 모든 뷰를 끄고 초기 뷰(Title)를 엽니다.
            foreach (var view in views)
            {
                view.Hide();
            }
            
            ChangeView(OutgameViewType.Title);
        }

        public void ChangeView(OutgameViewType targetViewType)
        {
            if (currentView != null)
            {
                currentView.Hide();
            }

            var nextView = views.Find(v => v.ViewType == targetViewType);
            if (nextView != null)
            {
                currentView = nextView;
                currentView.Show();
            }
            else
            {
                Debug.LogError($"[OutgameUIManager] 뷰를 찾을 수 없습니다: {targetViewType}");
            }
        }
    }
}
