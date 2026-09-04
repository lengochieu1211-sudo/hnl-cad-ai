using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HNL.VXT.Core.Utilities;

namespace HNL.VXT.UI.Controls
{
    public partial class HnlNumericBox : UserControl
    {
        private bool _syncing;
        private double _lastCommittedValue;

        public HnlNumericBox()
        {
            InitializeComponent();
            _lastCommittedValue = Value;
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
            var control = (HnlNumericBox)d;
            control._lastCommittedValue = (double)e.NewValue;
            control.SyncText();
        }

        private void Decrease_Click(object sender, RoutedEventArgs e)
        {
            ClearInputError();
            Value = Clamp(Value - Step);
        }

        private void Increase_Click(object sender, RoutedEventArgs e)
        {
            ClearInputError();
            Value = Clamp(Value + Step);
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;
            ClearInputError();

            // Keep the original live-update behavior for a plain number.
            // Expressions are committed only on Enter/LostFocus so typing 100+200*2
            // is not prematurely replaced by an intermediate result.
            if (NumericExpressionEvaluator.IsPlainNumber(ValueTextBox.Text, out var value))
                Value = Clamp(value);
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitExpression(restoreOnError: true);
        }

        private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (CommitExpression(restoreOnError: false))
                    Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ClearInputError();
                Value = _lastCommittedValue;
                SyncText();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private bool CommitExpression(bool restoreOnError)
        {
            var text = ValueTextBox.Text;
            if (NumericExpressionEvaluator.TryEvaluate(text, out var value))
            {
                ClearInputError();
                Value = Clamp(value);
                _lastCommittedValue = Value;
                SyncText();
                return true;
            }

            ShowInputError("Phép tính không hợp lệ. Ví dụ: 1220/3 hoặc (1200-100)/2");
            if (restoreOnError)
            {
                Value = _lastCommittedValue;
                SyncText();
            }
            return false;
        }

        private void ShowInputError(string message)
        {
            if (ValueTextBox == null) return;
            ValueTextBox.BorderBrush = Brushes.IndianRed;
            ValueTextBox.BorderThickness = new Thickness(1.5);
            ValueTextBox.ToolTip = message;
        }

        private void ClearInputError()
        {
            if (ValueTextBox == null) return;
            ValueTextBox.ClearValue(Border.BorderBrushProperty);
            ValueTextBox.ClearValue(Border.BorderThicknessProperty);
            ValueTextBox.ToolTip = "Nhập số hoặc phép tính, ví dụ: 1220/3, 600+25, 2*450, (1200-100)/2";
        }

        private void SyncText()
        {
            if (ValueTextBox == null) return;
            _syncing = true;
            ValueTextBox.Text = Value.ToString("0.##", CultureInfo.InvariantCulture);
            _syncing = false;
        }

        private double Clamp(double value) => Math.Max(Minimum, Math.Min(Maximum, value));
    }
}
