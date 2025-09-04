using Avalonia.Controls;

namespace KaktusCode;

public partial class MainWindow : Window
{
    private readonly KaktuscodeInterpreter _interpreter;

    public MainWindow()
    {
        InitializeComponent();
        _interpreter = new KaktuscodeInterpreter(this);
        RunButton.Click += OnRunClicked;
    }

    private async void OnRunClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var input = CodeInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(input))
        {
            OutputBox.Text = "⚠️ Please enter some code!";
            return;
        }

        OutputBox.Text = "⏳ Running...";
        var result = await _interpreter.Run(input);
        OutputBox.Text = result;
    }
}