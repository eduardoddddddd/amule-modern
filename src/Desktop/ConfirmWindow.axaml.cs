using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AmuleModern.Desktop;

public partial class ConfirmWindow : Window
{
    public bool Accepted { get; private set; }
    public ConfirmWindow() => InitializeComponent();
    public ConfirmWindow(string title, string body, string accept) : this()
    {
        Title = title;
        TitleText.Text = title;
        BodyText.Text = body;
        AcceptButton.Content = accept;
    }
    private void AcceptClicked(object? sender, RoutedEventArgs e) { Accepted = true; Close(); }
    private void CancelClicked(object? sender, RoutedEventArgs e) => Close();
}
