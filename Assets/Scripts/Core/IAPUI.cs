namespace CheckmateRPG.Core
{
    public interface IAPUI
    {
        void UpdateAP(float current, float max, float delta, APChangeReason reason);
        void ShowInsufficientAP(float requested, float current, float missing, APActionReason reason, string message);
    }
}
