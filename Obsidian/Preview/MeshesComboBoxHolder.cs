using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace Obsidian.Preview
{
    public class MeshesComboBoxHolder
    {
        public CheckBox CheckBox { get; }
        public TextBlock TextBlock { get; }
        public ComboBoxItem ComboBoxItem { get; }
        public GeometryModel3D Model { get; }
        public Model3DGroup ModelGroup { get; }
        public bool IsVisible { get; private set; }

        public MeshesComboBoxHolder(CheckBox checkBox, TextBlock textBlock, ComboBoxItem comboBoxItem, GeometryModel3D model, Model3DGroup modelGroup)
        {
            this.CheckBox = checkBox;
            this.TextBlock = textBlock;
            this.ComboBoxItem = comboBoxItem;
            this.Model = model;
            this.ModelGroup = modelGroup;
            this.IsVisible = true;
            this.CheckBox.Click += CheckBoxClicked;
            this.CheckBox.IsChecked = true;
        }

        private void CheckBoxClicked(object sender, RoutedEventArgs e)
        {
            if (this.IsVisible)
            {
                this.ModelGroup.Children.Remove(this.Model);
            }
            else
            {
                this.ModelGroup.Children.Add(this.Model);
            }

            this.IsVisible = !this.IsVisible;
        }
    }
}