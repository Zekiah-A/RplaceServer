using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TKOfficialGUI.Views;

public partial class ServerView : UserControl
{
    public ServerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}