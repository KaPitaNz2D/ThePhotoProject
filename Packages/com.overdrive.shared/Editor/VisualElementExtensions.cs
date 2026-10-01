using UnityEngine.UIElements;

namespace Overdrive
{
    /// <summary>
    /// Extension methods for VisualElement type.
    /// </summary>
    public static class VisualElementExtensions
    {
        /// <summary>
        /// The Queue extension method with added logging and setting of a bool if the requested element is missing.
        /// </summary>
        /// <typeparam name="T">Type of the required element.</typeparam>
        /// <param name="e">Element to search in.</param>
        /// <param name="name">Name of the element to find.</param>
        /// <param name="isElementMissing">Is set to true if the element is not found.</param>
        /// <returns>Found element or null.</returns>
        public static T QLog<T>(this VisualElement e, string name, ref bool isElementMissing) where T : VisualElement
        {
            if (e == null)
            {
                isElementMissing = true;
                return null;
            }

            var element = string.IsNullOrEmpty(name) ? e.Q<T>() : e.Q<T>(name);
            if (element == null)
            {
                UnityEngine.Debug.LogErrorFormat("UXML VisualElement '{0}' does not contains element '{1}'", e.name, name);
                isElementMissing = true;
                return null;
            }

            return element;
        }

        /// <summary>
        /// Sets the visibility of the VisualElement.<br/><br/>
        /// Works by setting VisualElement.style.display to either DisplayStyle.Flex or DisplayStyle.None.
        /// </summary>
        /// <param name="e">Element to influence.</param>
        /// <param name="isDisplayed">True is element should be visible.</param>
        public static void SetDisplay(this VisualElement e, bool isDisplayed)
        {
            if (e != null)
            {
                e.style.display = isDisplayed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Set the object type of an object field.<br/><br/>
        /// Nothing special, but allows to do field?.Set() instead of the brackets for null checking.
        /// </summary>
        public static void SetObjectType(this UnityEditor.UIElements.ObjectField e, System.Type t)
        {
            if (e != null)
            {
                e.objectType = t;
            }
        }
    }
}
