using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HNL.VXT.UI.Controls
{
    public partial class HnlNumericBox : UserControl
    {
        private bool _syncing;

        public HnlNumericBox()
        {
            InitializeComponent();
            SyncText();
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(HnlNumericBox),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum), typeof(double), typeof(HnlNumericBox), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(HnlNumericBox), new PropertyMetadata(1000000000.0));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step), typeof(double), typeof(HnlNumericBox), new PropertyMetadata(50.0));

        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
            nameof(Unit), typeof(string), typeof(HnlNumericBox), new PropertyMetadata("mm"));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, Clamp(value));
        }

        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
        public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HnlNumericBox)d).SyncText();
        }

        private void Decrease_Click(object sender, RoutedEventArgs e) => Value = Clamp(Value - Step);
        private void Increase_Click(object sender, RoutedEventArgs e) => Value = Clamp(Value + Step);

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            if (TryParse(ValueTextBox.Text, out var value))
                Value = Clamp(value);
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e) => CommitOrRestore();

        private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitOrRestore();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void CommitOrRestore()
        {
            if (TryParse(ValueTextBox.Text, out var value)) Value = Clamp(value);
            SyncText();
        }

        private void SyncText()
        {
            if (ValueTextBox == null) return;
            _syncing = true;
            ValueTextBox.Text = Value.ToString("0.##", CultureInfo.InvariantCulture);
            _syncing = false;
        }

        private double Clamp(double value) => Math.Max(Minimum, Math.Min(Maximum, value));

        private static bool TryParse(string text, out double value)
        {
            text = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
