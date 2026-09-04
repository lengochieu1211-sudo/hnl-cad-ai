using System;
using System.Globalization;

namespace HNL.VXT.Core.Utilities
{
    /// <summary>
    /// Small, deterministic arithmetic parser for VXT numeric inputs.
    /// Supports +, -, *, /, parentheses, unary signs, decimal comma/dot,
    /// and x/× as multiplication. No scripting/eval is used.
    /// </summary>
    public static class NumericExpressionEvaluator
    {
        public static bool TryEvaluate(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            try
            {
                var parser = new Parser(Normalize(text));
                value = parser.Parse();
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }
            catch
            {
                value = 0.0;
                return false;
            }
        }

        public static bool IsPlainNumber(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Normalize(string text)
        {
            return text
                .Trim()
                .Replace(',', '.')
                .Replace('×', '*')
                .Replace('x', '*')
                .Replace('X', '*')
                .Replace(':', '/');
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text) => _text = text;

            public double Parse()
            {
                SkipWhiteSpace();
                if (Peek('=')) _index++;
                var result = ParseExpression();
                SkipWhiteSpace();
                if (_index != _text.Length)
                    throw new FormatException("Unexpected character.");
                return result;
            }

            private double ParseExpression()
            {
                var value = ParseTerm();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('+')) value += ParseTerm();
                    else if (Match('-')) value -= ParseTerm();
                    else return value;
                }
            }

            private double ParseTerm()
            {
                var value = ParseFactor();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('*')) value *= ParseFactor();
                    else if (Match('/'))
                    {
                        var divisor = ParseFactor();
                        if (Math.Abs(divisor) < 1e-15)
                            throw new DivideByZeroException();
                        value /= divisor;
                    }
                    else return value;
                }
            }

            private double ParseFactor()
            {
                SkipWhiteSpace();

                if (Match('+')) return ParseFactor();
                if (Match('-')) return -ParseFactor();

                if (Match('('))
                {
                    var value = ParseExpression();
                    SkipWhiteSpace();
                    if (!Match(')')) throw new FormatException("Missing closing parenthesis.");
                    return value;
                }

                return ParseNumber();
            }

            private double ParseNumber()
            {
                SkipWhiteSpace();
                var start = _index;
                var hasDigit = false;
                var hasDot = false;

                while (_index < _text.Length)
                {
                    var c = _text[_index];
                    if (char.IsDigit(c))
                    {
                        hasDigit = true;
                        _index++;
                        continue;
                    }
                    if (c == '.' && !hasDot)
                    {
                        hasDot = true;
                        _index++;
                        continue;
                    }
                    break;
                }

                if (!hasDigit) throw new FormatException("Number expected.");

                // Scientific notation is useful for engineering data, e.g. 1.2e3.
                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    var expStart = _index++;
                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    var expDigits = _index;
                    while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                    if (expDigits == _index)
                    {
                        _index = expStart;
                    }
                }

                var token = _text.Substring(start, _index - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException("Invalid number.");
                return value;
            }

            private bool Match(char c)
            {
                SkipWhiteSpace();
                if (_index >= _text.Length || _text[_index] != c) return false;
                _index++;
                return true;
            }

            private bool Peek(char c)
            {
                SkipWhiteSpace();
                return _index < _text.Length && _text[_index] == c;
            }

            private void SkipWhiteSpace()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            }
        }
    }
}
