using System;
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
            foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.font = UiFontProvider.GetTmpFont();
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

            float duration = OutgameStyle.TransitionDuration;
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
    /// 공통 탐색 API(뒤로가기, 모달 관리, 이력)를 제공합니다.
    /// </summary>
    public class OutgameUIManager : MonoBehaviour
    {
        public static OutgameUIManager Instance { get; private set; }

        [SerializeField] private List<OutgameViewBase> views;
        
        private OutgameViewBase currentView;
        
        public event Action<OutgameViewType> OnViewChanged;
        public OutgameViewType CurrentViewType => currentView != null ? currentView.ViewType : OutgameViewType.Title;

        private static bool _isFirstLaunch = true;
        
        private Stack<OutgameViewType> _history = new Stack<OutgameViewType>();
        private Stack<Action> _modalCloseActions = new Stack<Action>();
        
        private float _lastTransitionTime = -1f;
        
        private bool IsTransitioning => Time.unscaledTime - _lastTransitionTime < OutgameStyle.TransitionDuration;

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
            // 모든 뷰를 끄고 초기 뷰(Title 또는 로비)를 엽니다.
            foreach (var view in views)
            {
                view.Hide();
            }
            
            if (_isFirstLaunch)
            {
                _isFirstLaunch = false;
                ChangeView(OutgameViewType.Title, true);
            }
            else
            {
                ChangeView(OutgameViewType.MainLobby, true);
            }
        }
        
        private void Update()
        {
            // 키보드 뒤로가기 탐색
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GoBack();
            }
        }

        public void ChangeView(OutgameViewType targetViewType, bool clearHistory = false)
        {
            if (IsTransitioning) return;
            _lastTransitionTime = Time.unscaledTime;
            
            // Both roster entry points share the same progression UI and TP rules.
            if (targetViewType == OutgameViewType.Roster)
                targetViewType = OutgameViewType.OperatorHub;
                
            var nextView = views.Find(v => v != null && v.ViewType == targetViewType);
            if (nextView == null)
            {
                Debug.LogWarning($"[OutgameUIManager] 뷰를 찾을 수 없습니다: {targetViewType}");
                return;
            }
            
            if (clearHistory)
            {
                _history.Clear();
            }
            else if (currentView != null && currentView.ViewType != targetViewType)
            {
                _history.Push(currentView.ViewType);
            }

            if (currentView != null)
            {
                currentView.Hide();
            }

            currentView = nextView;
            currentView.Show();
            OnViewChanged?.Invoke(currentView.ViewType);
        }
        
        /// <summary>
        /// 뒤로가기 탐색. 모달이 열려있으면 먼저 닫고, 아니면 이전 뷰로 돌아갑니다.
        /// 빠른 화면 전환 중에는 이중 입력을 막습니다.
        /// </summary>
        public void GoBack()
        {
            if (IsTransitioning) return;
            
            if (_modalCloseActions.Count > 0)
            {
                var closeAction = _modalCloseActions.Pop();
                closeAction?.Invoke();
                return;
            }
            
            if (_history.Count > 0)
            {
                _lastTransitionTime = Time.unscaledTime;
                var prevViewType = _history.Pop();
                var nextView = views.Find(v => v != null && v.ViewType == prevViewType);
                if (nextView != null)
                {
                    if (currentView != null) currentView.Hide();
                    currentView = nextView;
                    currentView.Show();
                    OnViewChanged?.Invoke(currentView.ViewType);
                }
            }
            else if (currentView != null && currentView.ViewType != OutgameViewType.MainLobby && currentView.ViewType != OutgameViewType.Title)
            {
                ChangeView(OutgameViewType.MainLobby);
            }
        }

        public void RegisterModalCloseAction(Action closeAction)
        {
            if (closeAction != null)
            {
                _modalCloseActions.Push(closeAction);
            }
        }

        public void UnregisterModalCloseAction(Action closeAction)
        {
            if (_modalCloseActions.Count > 0 && _modalCloseActions.Peek() == closeAction)
            {
                _modalCloseActions.Pop();
            }
        }
    }
}
