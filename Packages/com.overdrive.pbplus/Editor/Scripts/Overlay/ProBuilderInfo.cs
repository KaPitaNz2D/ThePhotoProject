
namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Controls the existence and visibility of the <see cref="ProBuilderInfoOverlay"/> according
    /// to status of <see cref="ProBuilderPlusCore"/>.
    /// </summary>
    public static class ProBuilderInfo
    {
        private static ProBuilderInfoOverlay instance;
        private static bool isOverlyAdded = false;

        /// <summary>
        /// Initializes this. Called from static ctor of ProBuilderCore.
        /// </summary>
        public static void Initialize()
        {
            instance = new ProBuilderInfoOverlay();
            ProBuilderPlusCore.OnStatusChanged += ProBuilderPlusCoreOnStatusChanged;

        }

        private static void ProBuilderPlusCoreOnStatusChanged()
        {
            if (instance == null)
            {
                return;
            }

            var toolMode = ProBuilderPlusCore.CurrentToolMode;
            instance.UpdateOverlay();
            if (toolMode.IsEditMode())
            {
                if (!isOverlyAdded)
                {
                    // Note: Do not change instance.displayed, let's have Unity remember visibility.
                    UnityEditor.SceneView.AddOverlayToActiveView(instance);
                    isOverlyAdded = true;
                }
            }
            else
            {
                if (isOverlyAdded)
                {
                    // Note: Do not change instance.displayed, let's have Unity remember visibility.
                    instance.MemorizeUiState();
                    UnityEditor.SceneView.RemoveOverlayFromActiveView(instance);
                    isOverlyAdded = false;
                }
            }
        }
    }
}
