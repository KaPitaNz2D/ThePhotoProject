using System;
using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Contains Information about actions based on <see cref="PreviewMenuAction"/>.<br/>
    /// Instances of type can only be created with the static CreateActionInfoFromAttribute method with an attribute as a parameter.
    /// </summary>
    public sealed class ActionInfo : IActionInfo
    {
        private PreviewMenuAction previewActionInstance;

        /// <summary>
        /// Use Construction methods.
        /// </summary>
        private ActionInfo(ProBuilderPlusActionAttribute attribute)
        {
            this.CachedAttribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }

        public ProBuilderPlusActionAttribute CachedAttribute { get; }

        public string DisplayName => this.CachedAttribute.DisplayName;

        public Texture2D Icon { get; private set; }

        public string Id => this.CachedAttribute.Id;

        public bool IsEnabledForCurretMode => this.previewActionInstance == null || previewActionInstance.IsInValidMode;

        public int Order => this.CachedAttribute.Order;

        public bool SupportsInstantMode => this.CachedAttribute.SupportsInstantMode;

        public string Tooltip => this.CachedAttribute.Tooltip ?? this.CachedAttribute.DisplayName;

        public void ExecuteAction()
        {
            if (this.previewActionInstance != null)
            {
                PreviewActionFramework.HandleAction(this.previewActionInstance);
            }
        }

        public void ExecuteInstant()
        {
            if (this.previewActionInstance != null)
            {
                this.previewActionInstance.StartPreview();

                // Todo: CortiWins : Log this UnityEngine.ProBuilder.ActionResult
                UnityEngine.ProBuilder.ActionResult result = this.previewActionInstance.ApplyChanges();
                if (result.status == UnityEngine.ProBuilder.ActionResult.Status.Failure)
                {
                    Debug.LogErrorFormat("Action '{0}.{1}' ApplyChanges execute Instant with 'Status.Failure' Message: {2}", this.Id, this.DisplayName, result.notification);
                }

                this.previewActionInstance.CleanupPreview();
            }
        }

        /// <summary>
        /// Create ActionInfo from a type and its attribute.
        /// </summary>
        internal static ActionInfo CreateActionInfoFromAttribute(Type actionType, ProBuilderPlusActionAttribute attribute)
        {
            try
            {


                if (!typeof(PreviewMenuAction).IsAssignableFrom(actionType))
                {
                    Debug.LogError($"ActionInfo for {actionType.Name} can not be created. Invalid type.");
                    return null;
                }

                var actionInfo = new ActionInfo(attribute)
                {
                    Icon = ProBuilderPlusActions.LoadIcon(attribute.IconPath),
                    previewActionInstance = Activator.CreateInstance(actionType) as PreviewMenuAction,
                };

                actionInfo.previewActionInstance.SetCachedAttribute(actionInfo.CachedAttribute);
                return actionInfo;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating ActionInfo for {actionType.Name}: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }
    }
}
