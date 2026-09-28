using System.Collections.Generic;
using System.Reflection;
using CheckmateRPG.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.Tests
{
    public sealed class NavigationTestView : OutgameViewBase
    {
        public override void Show() { gameObject.SetActive(true); }
        public override void Hide() { gameObject.SetActive(false); }
    }

    public sealed class OutgameNavigationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject Make(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); objects.Add(go); return go;
        }

        private OutgameUIManager Manager()
        {
            var manager = Make("Router").AddComponent<OutgameUIManager>();
            typeof(OutgameUIManager).GetProperty("Instance").SetValue(null, manager);
            var views = new List<OutgameViewBase>();
            foreach (var type in new[] { OutgameViewType.MainLobby, OutgameViewType.DeckBuilder, OutgameViewType.StageSelect })
            {
                var view = Make(type.ToString()).AddComponent<NavigationTestView>(); view.ViewType = type; views.Add(view);
            }
            typeof(OutgameUIManager).GetField("views", Private).SetValue(manager, views);
            return manager;
        }

        private static void EndTransition(OutgameUIManager manager)
        {
            typeof(OutgameUIManager).GetField("_lastTransitionTime", Private).SetValue(manager, -100f);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in objects) if (go) Object.DestroyImmediate(go);
            objects.Clear();
            typeof(OutgameUIManager).GetProperty("Instance").SetValue(null, null);
        }

        [Test]
        public void LobbyFormationButton_OpensDeckBuilder()
        {
            var manager = Manager();
            var go = Make("LobbyBindings"); go.SetActive(false);
            var lobby = go.AddComponent<MainLobbyUI>();
            var button = Make("Formation").AddComponent<Button>();
            button.gameObject.AddComponent<Image>();
            typeof(MainLobbyUI).GetField("rosterButton", Private).SetValue(lobby, button);
            typeof(MainLobbyUI).GetMethod("Awake", Private).Invoke(lobby, null);
            button.onClick.Invoke();
            Assert.AreEqual(OutgameViewType.DeckBuilder, manager.CurrentViewType);
        }


        [Test]
        public void EdictSelector_BackClosesModalWithoutLeavingBoard()
        {
            var manager = Manager();
            manager.ChangeView(OutgameViewType.StageSelect);
            var modal = Make("Selector").AddComponent<EdictSelectorModal>();
            modal.EnsureRuntimeUI();
            modal.OpenSelector(CheckmateRPG.Progression.EdictKind.Absolute, _ => { });
            EndTransition(manager);
            manager.GoBack();
            Assert.IsFalse(modal.transform.Find("ModalPanel").gameObject.activeSelf);
            Assert.AreEqual(OutgameViewType.StageSelect, manager.CurrentViewType);
        }

        [Test]
        public void LeavingView_ClosesOverlay_AndBackReturnsToPreviousView()
        {
            var manager = Manager();
            manager.ChangeView(OutgameViewType.StageSelect);
            bool modalOpen = true;
            manager.RegisterModalCloseAction(() => modalOpen = false);
            EndTransition(manager);
            manager.ChangeView(OutgameViewType.DeckBuilder);
            Assert.IsFalse(modalOpen, "The previous screen's overlay must close when navigating.");
            EndTransition(manager);
            manager.GoBack();
            Assert.AreEqual(OutgameViewType.StageSelect, manager.CurrentViewType, "ESC must navigate rather than consume a stale overlay action.");
        }
    }
}
