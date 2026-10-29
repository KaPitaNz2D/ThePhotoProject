
namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Traits for ActionInfo types for dfferent types of actions.
    /// </summary>
    public interface IActionInfo
    {
        public string DisplayName { get; }

        public UnityEngine.Texture2D Icon { get; }

        public string Id { get; }

        public bool IsEnabledForCurretMode { get; }

        public int Order { get; }

        public bool SupportsInstantMode { get; }

        public string Tooltip { get; }

        public void ExecuteAction();

        public void ExecuteInstant();
    }
}
