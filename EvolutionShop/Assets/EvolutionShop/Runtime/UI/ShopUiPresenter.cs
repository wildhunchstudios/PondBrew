using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Base for UI Toolkit screens. A presenter reads ShopSession state and calls ShopSession's existing
    /// commands; it never changes money, stock or saves directly.
    /// </summary>
    public abstract class ShopUiPresenter
    {
        protected ShopSession Shop { get; private set; }
        protected ShopUiStrings Strings { get; private set; }
        public VisualElement Element { get; private set; }
        public bool IsShown { get; private set; }

        public void Bind(ShopSession shop, VisualElement parent, VisualTreeAsset template, ShopUiStrings strings)
        {
            Shop = shop;
            Strings = strings ?? ShopUiStrings.Empty;
            Element = Build(template);
            parent.Add(Element);
            OnBound();
            Hide();
        }

        /// <summary>Creates this presenter's element from its template; called once by Bind.</summary>
        protected abstract VisualElement Build(VisualTreeAsset template);
        protected virtual void OnBound() { }
        /// <summary>Called every frame by ShopUiRoot after gameplay has updated.</summary>
        public abstract void Refresh();

        public virtual void Unbind()
        {
            Element?.RemoveFromHierarchy();
            Element = null;
            Shop = null;
            IsShown = false;
        }

        protected void Show()
        {
            if (Element == null) return;
            Element.style.display = DisplayStyle.Flex;
            IsShown = true;
        }

        public void Hide()
        {
            if (Element == null) return;
            Element.style.display = DisplayStyle.None;
            IsShown = false;
        }

        /// <summary>Takes the first element with the given name out of a template instance.</summary>
        protected static VisualElement Extract(VisualTreeAsset template, string name)
        {
            var tree = template.Instantiate();
            var element = tree.Q<VisualElement>(name) ?? tree;
            element.RemoveFromHierarchy();
            return element;
        }
    }
}
