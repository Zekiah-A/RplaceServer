using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TKOfficialGUI.Views;

public partial class PlayerProfile : UserControl
{
    public PlayerProfile()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}