using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public sealed class EconomyDialog : MonoBehaviour
    {
        private TextMeshProUGUI _body;
        private bool _cores;
        public static void Open(bool cores=false, string message="")
        {
            var existing=FindFirstObjectByType<EconomyDialog>();
            if(existing){existing.Close();}
            var root=new GameObject("EconomyDialog",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var rootRect=root.GetComponent<RectTransform>();
            rootRect.anchorMin=Vector2.zero; rootRect.anchorMax=Vector2.one;
            rootRect.offsetMin=Vector2.zero; rootRect.offsetMax=Vector2.zero;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var modal=root.AddComponent<EconomyDialog>();modal._cores=cores;modal.Build(message);
            OutgameUIManager.Instance?.RegisterModalCloseAction(modal.Close);
        }
        private void Build(string message)
        {
            var shade=TacticalLayout.Panel(transform,"Shade",new Color(0,0,0,.8f),0,0,1,1);shade.raycastTarget=true;
            var card=TacticalLayout.Panel(transform,"Card",OutgameStyle.SurfaceDark,.23f,.2f,.54f,.6f).transform;
            TacticalLayout.Label(card,"Title",_cores?"공명석 보급 / ORIGIN EXCHANGE":"PRTS ALERT / 전력 충전",32,OutgameStyle.TextWhite,.05f,.82f,.9f,.1f);
            _body=TacticalLayout.Label(card,"Body","",22,OutgameStyle.TextWhite,.05f,.35f,.9f,.43f);_body.textWrappingMode=TextWrappingModes.Normal;
            if(_cores)Button(card,"코어 1 → 공명석 100",.05f,.22f,.9f,()=>Act(0));
            else
            {
                Button(card,"소형 +30",.05f,.24f,.28f,()=>Act(30));
                Button(card,"중형 +60",.36f,.24f,.28f,()=>Act(60));
                Button(card,"대형 +120",.67f,.24f,.28f,()=>Act(120));
                Button(card,"공명석으로 전력 +60",.05f,.09f,.59f,()=>Act(0));
            }
            Button(card,"닫기",.67f,.09f,.28f,Close);Refresh(message);
        }
        private static void Button(Transform parent,string label,float x,float y,float w,UnityEngine.Events.UnityAction action)
        {
            var panel=TacticalLayout.Panel(parent,label,OutgameStyle.Paper,x,y,w,.105f);panel.raycastTarget=true;
            var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel;button.onClick.AddListener(action);
            var text=TacticalLayout.Label(panel.transform,"Label",label,20,OutgameStyle.Ink,.03f,0,.94f,1);text.alignment=TextAlignmentOptions.Center;
            OutgameStyle.ApplyLightButtonStyle(button,text);
        }
        private void Act(int amount)
        {
            var sm=SaveManager.EnsureInstance();string reason;
            bool ok=_cores?EconomyService.TryExchangeCore(sm.CurrentData,out reason):amount==0?EconomyService.TryRecharge(sm.CurrentData,DateTime.UtcNow,out reason):EconomyService.TryUseRation(sm.CurrentData,amount,DateTime.UtcNow,out reason);
            if(ok)sm.SaveGame();Refresh(ok?"보급이 완료되었습니다.":reason);
        }
        private void Refresh(string message)
        {
            var s=SaveManager.EnsureInstance().CurrentData;
            int cost=EconomyService.RechargeCost(s,DateTime.UtcNow);
            _body.text=_cores?$"보유 오리진 코어  {s.PaidOriginCores:N0}\n공명석  {s.ResonanceCrystals:N0}\n\n코어 1개를 소비하여 공명석 100개로 환전합니다.\n현금 결제 기능은 연결되지 않았습니다.\n{message}":$"인가 전력  {s.Sanity} / {s.MaxSanity}     공명석  {s.ResonanceCrystals:N0}\n비상 팩  소형 {s.SanityRationsSmall} · 중형 {s.SanityRationsMedium} · 대형 {s.SanityRationsLarge}\n다음 충전 비용  {(cost<0?"오늘 충전 완료":cost+" 공명석")}\n일일 충전 횟수는 UTC 00:00에 초기화됩니다.\n{message}";
        }
        public void Close(){OutgameUIManager.Instance?.UnregisterModalCloseAction(Close);Destroy(gameObject);}
    }
}
