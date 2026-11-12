using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Halo.Launcher;

internal static class LauncherCalc
{
    internal readonly record struct Answer(string Shown, string Copy);

    internal static Answer? Try(string? query)
    {
        string q = Normalise(query ?? "");
        if (q.Length == 0) return null;
        return Convert(q) ?? Evaluate(q);
    }

    private static string Normalise(string s)
    {
        var b = new StringBuilder(s.Length);
        foreach (char c in s.Trim())
        {
            if (c is >= '\u06F0' and <= '\u06F9') b.Append((char)('0' + (c - '\u06F0')));
            else if (c is >= '\u0660' and <= '\u0669') b.Append((char)('0' + (c - '\u0660')));
            else if (c == '\u066B') b.Append('.');
            else if (c == '\u00D7') b.Append('*');
            else if (c == '\u00F7') b.Append('/');
            else if (c != ',' && c != '\u066C') b.Append(c);
        }
        return b.ToString();
    }

    internal static string Format(double v)
    {

        double r = double.Parse(v.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (r == 0d) r = 0d;
        return Math.Abs(r) < 1e15 && Math.Abs(r) >= 1e-6 || r == 0d
            ? r.ToString("#,0.##########", CultureInfo.InvariantCulture)
            : r.ToString("0.#####E+0", CultureInfo.InvariantCulture);
    }

    private static Answer Make(double v, string suffix = "")
    {
        string shown = Format(v);
        return new Answer(shown + suffix, shown.Replace(",", ""));
    }

    private static Answer? Evaluate(string q)
    {
        try
        {
            var p = new Parser(q);
            double v = p.Run();

            if (!p.Worked || double.IsNaN(v) || double.IsInfinity(v)) return null;
            return Make(v);
        }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;
        private int _depth;
        private const int MaxDepth = 64;
        internal bool Worked { get; private set; }

        internal Parser(string s) { _s = s; }

        internal double Run()
        {
            double v = Sum();
            Skip();
            if (_i != _s.Length) throw new FormatException();
            return v;
        }

        private void Skip() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

        private bool Eat(char c)
        {
            Skip();
            if (_i >= _s.Length || _s[_i] != c) return false;
            _i++;
            return true;
        }

        private double Sum()
        {
            double v = Product();
            while (true)
            {
                if (Eat('+')) { v += Product(); Worked = true; }
                else if (Eat('-')) { v -= Product(); Worked = true; }
                else return v;
            }
        }

        private double Product()
        {
            double v = Power();
            while (true)
            {
                Skip();
                if (Eat('*')) { v *= Power(); Worked = true; }
                else if (Eat('/')) { v /= Power(); Worked = true; }

                else if (_i < _s.Length && _s[_i] is 'x' or 'X' && _i > 0 && char.IsDigit(_s[_i - 1])
                         && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1]))
                {
                    _i++;
                    v *= Power();
                    Worked = true;
                }
                else return v;
            }
        }

        private double Power()
        {
            double b = Unary();
            if (Eat('^')) { Worked = true; return Math.Pow(b, Power()); }
            return b;
        }

        private double Unary()
        {
            if (Eat('-')) return -Unary();
            if (Eat('+')) return Unary();
            double v = Atom();
            while (Eat('%')) { v /= 100d; Worked = true; }
            return v;
        }

        private double Atom()
        {
            Skip();
            if (_i >= _s.Length) throw new FormatException();
            if (++_depth > MaxDepth) throw new FormatException();
            try
            {
                if (Eat('('))
                {
                    double v = Sum();
                    if (!Eat(')')) throw new FormatException();
                    return v;
                }
                if (char.IsLetter(_s[_i])) return Word();
                return Number();
            }
            finally { _depth--; }
        }

        private double Number()
        {
            int start = _i;
            while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
            if (start == _i) throw new FormatException();
            return double.Parse(_s[start.._i], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private double Word()
        {
            int start = _i;
            while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
            string w = _s[start.._i].ToLowerInvariant();

            if (w == "pi") return Math.PI;
            if (w == "e") return Math.E;

            Func<double, double>? f = w switch
            {
                "sqrt" => Math.Sqrt,
                "abs" => Math.Abs,
                "round" => x => Math.Round(x, MidpointRounding.AwayFromZero),
                "floor" => Math.Floor,
                "ceil" => Math.Ceiling,
                "sin" => Math.Sin,
                "cos" => Math.Cos,
                "tan" => Math.Tan,
                "ln" => Math.Log,
                "log" => Math.Log10,
                _ => null,
            };
            if (f is null || !Eat('(')) throw new FormatException();
            double v = Sum();
            if (!Eat(')')) throw new FormatException();
            Worked = true;
            return f(v);
        }
    }

    private readonly record struct Unit(string Kind, double Factor, string Name);

    private static readonly Dictionary<string, Unit> Units = Build();

    private static Dictionary<string, Unit> Build()
    {
        var d = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
        void Add(string kind, double factor, string name, params string[] spellings)
        {
            d[name] = new Unit(kind, factor, name);
            foreach (var s in spellings) d[s] = new Unit(kind, factor, name);
        }

        Add("length", 1d, "m", "meter", "meters", "metre", "metres");
        Add("length", 1000d, "km", "kilometer", "kilometers", "kilometre", "kilometres");
        Add("length", 0.01d, "cm", "centimeter", "centimeters");
        Add("length", 0.001d, "mm", "millimeter", "millimeters");
        Add("length", 1609.344d, "mi", "mile", "miles");
        Add("length", 0.3048d, "ft", "foot", "feet");
        Add("length", 0.0254d, "in", "inch", "inches");
        Add("length", 0.9144d, "yd", "yard", "yards");

        Add("mass", 1d, "g", "gram", "grams");
        Add("mass", 1000d, "kg", "kilo", "kilos", "kilogram", "kilograms");
        Add("mass", 0.001d, "mg", "milligram", "milligrams");
        Add("mass", 1_000_000d, "t", "ton", "tons", "tonne", "tonnes");
        Add("mass", 453.59237d, "lb", "lbs", "pound", "pounds");
        Add("mass", 28.349523125d, "oz", "ounce", "ounces");

        Add("data", 1d, "B", "byte", "bytes");
        Add("data", 1e3, "KB");
        Add("data", 1e6, "MB");
        Add("data", 1e9, "GB");
        Add("data", 1e12, "TB");
        Add("data", 1024d, "KiB");
        Add("data", 1_048_576d, "MiB");
        Add("data", 1_073_741_824d, "GiB");
        Add("data", 1_099_511_627_776d, "TiB");

        Add("time", 0.001d, "ms");
        Add("time", 1d, "s", "sec", "secs", "second", "seconds");
        Add("time", 60d, "min", "mins", "minute", "minutes");
        Add("time", 3600d, "h", "hr", "hrs", "hour", "hours");
        Add("time", 86400d, "day", "days");
        Add("time", 604800d, "week", "weeks");

        Add("speed", 1d, "m/s");
        Add("speed", 1d / 3.6d, "km/h", "kmh", "kph");
        Add("speed", 0.44704d, "mph");
        Add("speed", 0.514444d, "knot", "knots", "kn");

        Add("temp", 1d, "C", "celsius", "\u00B0c");
        Add("temp", 1d, "F", "fahrenheit", "\u00B0f");
        Add("temp", 1d, "K", "kelvin");
        return d;
    }

    private static readonly Regex Conversion = new(
        @"^(-?\d+(?:\.\d+)?)\s*([a-z\u00B0/]+)\s+(?:to|in|as|=)\s+([a-z\u00B0/]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private static Answer? Convert(string q)
    {
        Match m;
        try { m = Conversion.Match(q); }
        catch (RegexMatchTimeoutException) { return null; }
        if (!m.Success) return null;

        if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return null;
        if (!Units.TryGetValue(m.Groups[2].Value, out var from) || !Units.TryGetValue(m.Groups[3].Value, out var to))
            return null;
        if (from.Kind != to.Kind) return null;

        double result = from.Kind == "temp"
            ? FromKelvin(ToKelvin(value, from.Name), to.Name)
            : value * from.Factor / to.Factor;
        return Make(result, " " + to.Name);
    }

    private static double ToKelvin(double v, string unit) => unit switch
    {
        "C" => v + 273.15d,
        "F" => (v - 32d) * 5d / 9d + 273.15d,
        _ => v,
    };

    private static double FromKelvin(double k, string unit) => unit switch
    {
        "C" => k - 273.15d,
        "F" => (k - 273.15d) * 9d / 5d + 32d,
        _ => k,
    };
}
