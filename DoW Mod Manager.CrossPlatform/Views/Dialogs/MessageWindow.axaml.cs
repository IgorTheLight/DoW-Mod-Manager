using Avalonia.Controls;

namespace DoW_Mod_Manager.Views.Dialogs;

/// <summary>Modal message / confirmation box (legacy <c>MessageForm</c> and <c>DialogueForm</c>).</summary>
public partial class MessageWindow : Window
{
    // Required by the XAML loader / designer.
    public MessageWindow() => InitializeComponent();

    public MessageWindow(string message, string title, bool withCancel) : this()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "DoW Mod Manager" : title;
        MessageText.Text = message;
        CancelButton.IsVisible = withCancel;
        OkButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}
