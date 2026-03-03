using System.Windows.Media;
using Frosty.Core;
using ClothDataPlugin.Windows;

namespace ClothDataPlugin.Extensions
{
    /// <summary>
    /// Menu extension that adds "Cloth Data Generator" to the Tools menu
    /// </summary>
    public class ClothDataMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Cloth Data Generator";
        public override ImageSource Icon => null;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            // Open the cloth data generator window
            var window = new ClothDataGeneratorWindow();
            window.ShowDialog();
        });
    }
}
