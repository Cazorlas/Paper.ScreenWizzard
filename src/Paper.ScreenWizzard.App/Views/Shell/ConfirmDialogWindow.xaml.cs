using System.Windows;
using Paper.ScreenWizzard.App.Views.Shell.Services;

namespace Paper.ScreenWizzard.App.Views.Shell;

/// <summary>A modal Yes/No question; <c>ShowDialog</c> returns true for Yes and false for No or Esc.</summary>
public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(string title, string question)
    {
        InitializeComponent();
        Title = title;
        QuestionText.Text = question;
        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
    }

    private void OnYesClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnNoClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
