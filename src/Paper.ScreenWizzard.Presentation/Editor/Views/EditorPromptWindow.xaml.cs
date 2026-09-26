using System.Windows;
using System.Windows.Automation;
using Paper.ScreenWizzard.Presentation.Shell.Views;

namespace Paper.ScreenWizzard.Presentation.Editor.Views;

/// <summary>A modal question with three answers; <see cref="Choice"/> is 0, 1 or 2 (Esc, the X and the third button are all 2).</summary>
public partial class EditorPromptWindow : Window
{
    public EditorPromptWindow(string title, string question, string primary, string secondary, string cancel)
    {
        InitializeComponent();
        Title = title;
        QuestionText.Text = question;

        // The buttons carry their text as content and as their accessible name, which is what the tests and a screen reader look for.
        foreach (var (button, text) in new[] { (PrimaryButton, primary), (SecondaryButton, secondary), (CancelButton, cancel) })
        {
            button.Content = text;
            AutomationProperties.SetName(button, text);
        }

        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
    }

    /// <summary>0 = the main answer, 1 = the other answer, 2 = back out. Backing out until a button is pressed.</summary>
    public int Choice { get; private set; } = 2;

    private void OnPrimaryClick(object sender, RoutedEventArgs e) => Answer(0);

    private void OnSecondaryClick(object sender, RoutedEventArgs e) => Answer(1);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Answer(2);

    private void Answer(int choice)
    {
        Choice = choice;
        DialogResult = choice != 2;
    }
}
