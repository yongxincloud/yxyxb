using System.Windows;
using System.Windows.Controls;

namespace Yxy.Client.Pages
{
    public static class PlaceholderPage
    {
        public static FrameworkElement Create(string title, string body)
        {
            var card = new Border
            {
                Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBg"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["CardLine"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            });
            card.Child = panel;
            return card;
        }
    }
}
