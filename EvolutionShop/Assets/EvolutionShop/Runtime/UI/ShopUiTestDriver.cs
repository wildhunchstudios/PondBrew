#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Development-only helper for UI tests: find elements by name and activate buttons through the panel's
    /// own event dispatch (a navigation submit, the same path keyboard/gamepad "submit" uses). It replaces
    /// clicking fixed screen coordinates. Physical mouse clicks still need a hands-on check.
    /// </summary>
    public static class ShopUiTestDriver
    {
        public static T Find<T>(VisualElement root, string name) where T : VisualElement => root?.Q<T>(name);

        public static bool Submit(Button button)
        {
            if (button == null || button.panel == null || !button.enabledInHierarchy) return false;
            if (button.resolvedStyle.display == DisplayStyle.None) return false;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            return true;
        }

        /// <summary>True when the element's rendered rectangle lies fully inside the root's rectangle.</summary>
        public static bool FullyInside(VisualElement element, VisualElement root)
        {
            if (element == null || root == null) return false;
            var inner = element.worldBound; var outer = root.worldBound;
            return inner.width > 0 && inner.height > 0 && inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f &&
                inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;
        }
    }
}
#endif
