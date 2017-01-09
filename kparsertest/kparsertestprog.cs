/*
        KPARSER PROJECT C# VERSION

    Utilities library for parsers programming

    (c) livingcreative, 2017

    https://github.com/livingcreative/kparsercs

    kparsertestprog.cs
        Simple program for checking/testing library classes and
        functionality
*/

using System;
using KParserCS;

namespace KParserCSTest
{
    class TestProgram
    {
        // just convert bool as readable PASSED/FAILED result value
        static string TestResult(bool result)
        {
            return result ? "PASSED" : "FAILED";
        }

        // default decoration for known values
        static string decorate<T>(T value)
        {
            if (typeof(T) == typeof(string))
                return $"\"{value}\"";
            else if (typeof(T) == typeof(char))
                return $"'{value}'";
            else
                return value.ToString();
        }

        // execute test case function, compare result with expected value
        // and print formatted info message
        static void TestCase<T, D>(string name, Func<D, T> test, T expected, D data, Func<T, T, bool> compare = null, Func<T, string> decoratefunc = null)
        {
            var result = test(data);

            if (compare == null)
                compare = (a, b) => a.Equals(b);

            if (decoratefunc == null)
                decoratefunc = decorate;

            Console.Write(string.Format(name, data).PadRight(42));

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Actual:   ");
            Console.ForegroundColor = ConsoleColor.White;
            var actualvalue = decoratefunc(result).PadRight(20);
            Console.Write(actualvalue);

            if (actualvalue.Length > 20)
                Console.Write("\n".PadRight(43));

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Expected: ");
            Console.ForegroundColor = ConsoleColor.White;
            var expectedvalue = decoratefunc(expected).PadRight(20);
            Console.Write(expectedvalue);

            if (expectedvalue.Length > 20)
                Console.Write("\n".PadRight(43));

            Console.ResetColor();
            Console.Write("Result:   ");

            var compareresult = compare(result, expected);
            Console.ForegroundColor = compareresult ? ConsoleColor.Green : ConsoleColor.Red;

            Console.WriteLine(TestResult(compareresult));

            Console.ResetColor();
        }

        // short version for TestCase without additional test data and
        // default compare and decorator functions
        static void TestCase<T>(string name, Func<T> test, T expected)
        {
            TestCase(name, (d) => test(), expected, 0);
        }

        // short version for TestCase without additional test data
        static void TestCase<T>(string name, Func<T> test, T expected, Func<T, T, bool> compare = null, Func<T, string> decoratefunc = null)
        {
            TestCase(name, (d) => test(), expected, 0, compare, decoratefunc);
        }

        // execute test cases for ScannerStringSource class
        static void TestScannerStringSource()
        {
            var source = "abcdEFGH";

            IScannerSource src = new ScannerStringSource(source);

            TestCase(
                "Initial Position",
                () => src.Position,
                0
            );

            TestCase(
                "Initial Length",
                () => src.Length,
                source.Length
            );

            for (var n = 0; n < 4; ++n)
            {
                TestCase(
                    "CharCurrent",
                    () =>
                    {
                        var result = src.CharCurrent;
                        src.Advance();
                        return result;
                    },
                    source[n]
                );
            }

            TestCase(
                "Position after Advance()",
                () => src.Position,
                4
            );

            for (var n = 0; n < 4; ++n)
            {
                TestCase(
                    "CharAt({0})",
                    (d) => src.CharAt(n),
                    source[src.Position + n],
                    n
                );
            }


            TestCase(
                "IsEnd value",
                () =>
                {
                    src.Advance(4);
                    return src.IsEnd;
                },
                true
            );

            TestCase(
                "TokenToString(<{0}>)",
                (d) => src.TokenToString(d),
                source.Substring(2, 4),
                new SourceToken(2, 4)
            );

            Console.WriteLine();
        }

        // Scanner test class
        class ScannerTestClass : Scanner
        {
            private ScannerTestClass(ScannerStringSource source) :
                base(source)
            { }

            // convert char value to \xXXXX representation
            private string CharCode(char c)
            {
                return $"\\x{((int)c).ToString("X")}";
            }

            // execute test cases for Scanner.CharSet helper class
            public static void TestCharSet()
            {
                var charset = new CharSet();
                charset.Add('A', 'Z');
                charset.Add('a', 'z');
                charset.Add('_');

                TestCase(
                    "Contains: {0}",
                    (d) =>
                    {
                        var result = 0;

                        foreach (var c in d)
                        {
                            if (charset.Contains(c))
                                ++result;
                        }

                        return result;
                    },
                    8,
                    "heloTES_"
                );

                TestCase(
                    "Does not contain: {0}",
                    (d) =>
                    {
                        var result = 0;

                        foreach (var c in d)
                        {
                            if (!charset.Contains(c))
                                ++result;
                        }

                        return result;
                    },
                    8,
                    "61=^;.!0"
                );

                var charset2 = new Scanner.CharSet(charset);
                charset.Add('0', '9');

                TestCase(
                    "Contains: {0}",
                    (d) =>
                    {
                        var result = 0;

                        foreach (var c in d)
                        {
                            if (charset.Contains(c))
                                ++result;
                        }

                        return result;
                    },
                    8,
                    "_F0hT7bZ"
                );

                TestCase(
                    "Does not contain: {0}",
                    (d) =>
                    {
                        var result = 0;

                        foreach (var c in d)
                        {
                            if (!charset.Contains(c))
                                ++result;
                        }

                        return result;
                    },
                    8,
                    ";=(%!.,{"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.IsSpace property
            public static void TestIsSpace()
            {
                var n = 0;
                var text = " \t\0\a\r\n1A";
                var source = new ScannerStringSource(text);
                var results = new bool[]
                {
                    true, true, true, true,
                    false, false, false, false
                };

                var test = new ScannerTestClass(source);

                foreach (var c in text)
                {
                    TestCase(
                        $"IsSpace at '{test.CharCode(test.CharCurrent)}'",
                        () => test.IsSpace,
                        results[n]
                    );

                    source.Advance(1);

                    ++n;
                }

                Console.WriteLine();
            }

            // execute test cases for Scanner.IsBreak property
            public static void TestIsBreak()
            {
                var n = 0;
                var text = "\r \nZ\r\n\n\r";
                var source = new ScannerStringSource(text);
                var results = new int[] {  1, 0, 1, 0, 2, 2 };

                var test = new ScannerTestClass(source);

                while (!source.IsEnd)
                {
                    TestCase(
                        $"IsBreak at '{test.CharCode(test.CharCurrent)}'",
                        () => test.IsBreak,
                        results[n]
                    );

                    source.Advance(results[n] == 0 ? 1 : results[n]);

                    ++n;
                }

                Console.WriteLine();
            }

            // execute test cases for Scaner.Match functions
            public static void TestMatch()
            {
                TestCase(
                    "Match({0})",
                    (d) => Match(d),
                    false,
                    ScanResult.NoMatch
                );
                TestCase(
                    "Match({0})",
                    (d) => Match(d),
                    true,
                    ScanResult.Match
                );
                TestCase(
                    "Match({0})",
                    (d) => Match(d),
                    true,
                    ScanResult.MatchTrimmedEOL
                );
                TestCase(
                    "Match({0})",
                    (d) => Match(d),
                    true,
                    ScanResult.MatchTrimmedEOF
                );

                TestCase(
                    "NotMatch({0})",
                    (d) => NotMatch(d),
                    true,
                    ScanResult.NoMatch
                );
                TestCase(
                    "NotMatch({0})",
                    (d) => NotMatch(d),
                    false,
                    ScanResult.Match
                );
                TestCase(
                    "NotMatch({0})",
                    (d) => NotMatch(d),
                    false,
                    ScanResult.MatchTrimmedEOL
                );
                TestCase(
                    "NotMatch({0})",
                    (d) => NotMatch(d),
                    false,
                    ScanResult.MatchTrimmedEOF
                );

                TestCase(
                    "AnyMatch(<no match>)",
                    (d) =>
                    {
                        SourceToken token;
                        var result = AnyMatch(
                            out token,
                            (out SourceToken t) => { t = d.Token; return d.Result; },
                            (out SourceToken t) => { t = d.Token; return d.Result; },
                            (out SourceToken t) => { t = d.Token; return d.Result; },
                            (out SourceToken t) => { t = d.Token; return d.Result; }
                        );
                        return new { Token = token, Result = result };
                    },
                    new { Token = new SourceToken(), Result = ScanResult.NoMatch },
                    new { Token = new SourceToken(), Result = ScanResult.NoMatch },
                    (a, b) => a.Token.Equals(b.Token) && a.Result == b.Result,
                    (v) => $"{{{v.Token}, {v.Result}}}"
                );

                TestCase(
                    "AnyMatch(<some match>)",
                    (d) =>
                    {
                        SourceToken token;
                        var result = AnyMatch(
                            out token,
                            (out SourceToken t) => { t = new SourceToken(); return ScanResult.NoMatch; },
                            (out SourceToken t) => { t = new SourceToken(); return ScanResult.NoMatch; },
                            (out SourceToken t) => { t = d.Token; return d.Result; },
                            (out SourceToken t) => { t = new SourceToken(); return ScanResult.NoMatch; }
                        );
                        return new { Token = token, Result = result };
                    },
                    new { Token = new SourceToken(10, 20), Result = ScanResult.MatchTrimmedEOL },
                    new { Token = new SourceToken(10, 20), Result = ScanResult.MatchTrimmedEOL },
                    (a, b) => a.Token.Equals(b.Token) && a.Result == b.Result,
                    (v) => $"{{{v.Token}, {v.Result}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.HasCharacters function
            public static void TestHasCharacters()
            {
                var text = "0123456789";
                var source = new ScannerStringSource(text);
                var data = new int[] { 1, 4, 8, 20 };

                var test = new ScannerTestClass(source);

                var n = 0;
                var results1 = new bool[] { true, true, true, false };
                foreach (var dv in data)
                {
                    TestCase(
                        "HasCharacters({0})",
                        (d) => test.HasCharacters(d),
                        results1[n],
                        dv
                    );

                    ++n;
                }

                n = 0;
                source.Advance(5);
                var results2 = new bool[] { true, true, false, false };
                foreach (var dv in data)
                {
                    TestCase(
                        "HasCharacters({0})",
                        (d) => test.HasCharacters(d),
                        results2[n],
                        dv
                    );

                    ++n;
                }

                Console.WriteLine();
            }

            // execute test cases for Scanner.SkipToToken functions
            public static void TestSkipToToken()
            {
                var text = " \t\bA\r\nB";
                var source = new ScannerStringSource(text);

                var test = new ScannerTestClass(source);

                TestCase(
                    "SkipToToken(false)",
                    (d) =>
                    {
                        var result = test.SkipToToken(false);
                        return new { Result = result, Current = test.CharCurrent };
                    },
                    new { Result = true, Current = 'A' },
                    0,
                    (a, b) => a.Result == b.Result && a.Current == b.Current,
                    (v) => $"{{{v.Result}, '{v.Current}'}}"
                );

                source.Advance(1);

                TestCase(
                    "SkipToToken(false) at EOL",
                    (d) =>
                    {
                        var pos = source.Position;
                        var result = test.SkipToToken(false);
                        return new { Result = result, Position = pos };
                    },
                    new { Result = false, Position = 4 },
                    0,
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "SkipToToken(true)",
                    (d) =>
                    {
                        var result = test.SkipToToken();
                        return new { Result = result, Current = test.CharCurrent };
                    },
                    new { Result = true, Current = 'B' },
                    0,
                    (a, b) => a.Result == b.Result && a.Current == b.Current,
                    (v) => $"{{{v.Result}, '{v.Current}'}}"
                );

                source.Advance(1);

                TestCase(
                    "SkipToToken(true) at EOF",
                    (d) =>
                    {
                        var pos = source.Position;
                        var result = test.SkipToToken();
                        return new { Result = result, Position = pos };
                    },
                    new { Result = false, Position = 7 },
                    0,
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.Check functions
            public static void TestCheck()
            {
                var text = "ABCDEFG";
                var source = new ScannerStringSource(text);
                var test = new ScannerTestClass(source);

                TestCase(
                    "Check('{0}')",
                    (d) =>
                    {
                        var result = test.Check(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = false, Position = 0 },
                    'a',
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "Check('{0}', false)",
                    (d) =>
                    {
                        var result = test.Check(d, false);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = true, Position = 0 },
                    'A',
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "Check('{0}')",
                    (d) =>
                    {
                        var result = test.Check(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = true, Position = 1 },
                    'A',
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "Check(\"{0}\")",
                    (d) =>
                    {
                        var result = test.Check(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = false, Position = 1 },
                    "ABC",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "Check(\"{0}\", false)",
                    (d) =>
                    {
                        var result = test.Check(d, false);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = true, Position = 1 },
                    "BCD",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "Check(\"{0}\")",
                    (d) =>
                    {
                        var result = test.Check(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = true, Position = 4 },
                    "BCD",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.CheckAny functions
            public static void TestCheckAny()
            {
                var text = "ABCDEFG";
                var source = new ScannerStringSource(text);
                var test = new ScannerTestClass(source);

                TestCase(
                    "CheckAny(\"{0}\")",
                    (d) =>
                    {
                        var result = test.CheckAny(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = NO_MATCH, Position = 0 },
                    "abcd",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny(\"{0}\", false)",
                    (d) =>
                    {
                        var result = test.CheckAny(d, false);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = 3, Position = 0 },
                    "DCBA",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny(\"{0}\")",
                    (d) =>
                    {
                        var result = test.CheckAny(d);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = 3, Position = 1 },
                    "DCBA",
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny(\"{0}\", out token, false)",
                    (d) =>
                    {
                        SourceToken token;
                        var result = test.CheckAny(d, out token, false);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(1, 1), Position = 1 },
                    "DCBA",
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny({{\"abc\", \"defg\"}})",
                    (d) =>
                    {
                        int length;
                        var result = test.CheckAny(d, out length);
                        var pos = source.Position;
                        return new { Result = result, Position = pos };
                    },
                    new { Result = NO_MATCH, Position = 1 },
                    new string[] { "abc", "defg" },
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny({{\"ABC\", \"BCD\", \"GH\"}}, false)",
                    (d) =>
                    {
                        int length;
                        var result = test.CheckAny(d, out length, false);
                        var pos = source.Position;
                        return new { Result = result, Length = length, Position = pos };
                    },
                    new { Result = 1, Length = 3, Position = 1 },
                    new string[] { "AB", "BCD", "GHIJ" },
                    (a, b) => a.Result == b.Result && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Length}, {v.Position}}}"
                );

                TestCase(
                    "CheckAny({{\"ABC\", \"BCD\", \"GH\"}}, out token)",
                    (d) =>
                    {
                        SourceToken token;
                        var result = test.CheckAny(d, out token);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(1, 3), Position = 4 },
                    new string[] { "AB", "BCD", "GHIJ" },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.GetCharToken function
            public static void TestGetCharToken()
            {
                var text = "ABC\r\n";
                var source = new ScannerStringSource(text);
                var test = new ScannerTestClass(source);

                TestCase(
                    "GetCharToken(<no EOL, no adv.>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.GetCharToken(false, null, out token, false);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(0, 1), Position = 0 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "GetCharToken(<no EOL, adv.>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.GetCharToken(false, null, out token);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(0, 1), Position = 1 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "GetCharToken(<no EOL, inner, adv.>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.GetCharToken(false, () => 2, out token);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(1, 2), Position = 3 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "GetCharToken(<no EOL, inner, adv.>) @ EOL",
                    () =>
                    {
                        SourceToken token;
                        var result = test.GetCharToken(false, () => 2, out token);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = false, Token = new SourceToken(3), Position = 3 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "GetCharToken(<EOL, adv.>) @ EOL",
                    () =>
                    {
                        SourceToken token;
                        var result = test.GetCharToken(true, null, out token);
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = true, Token = new SourceToken(3, 2), Position = 5 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.CheckCharToken function
            public static void TestCheckCharToken()
            {
                // TODO
            }

            // execute test cases for Scanner.FromSetWhile function
            public static void TestFromSetWhile()
            {
                var text = "_iDenTifier1.test # one line comment\r\n2nd line";
                var source = new ScannerStringSource(text);
                var test = new ScannerTestClass(source);

                var alpha = new CharSet();
                alpha.Add('a', 'z');
                alpha.Add('A', 'Z');
                alpha.Add('_');

                var alphanum = new CharSet(alpha);
                alphanum.Add('0', '9');

                var hash = new CharSet();
                hash.Add('#');

                var any = new CharSet();
                any.Add('\x1', '\xFFFF');

                TestCase(
                    "FromSetWhile(<ident, no adv., no inner>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromSetWhile(
                            alpha, alphanum, false, null,
                            out token, false
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(0, 12), Position = 0 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromSetWhile(<ident, adv., no inner>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromSetWhile(
                            alpha, alphanum, false, null,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(0, 12), Position = 12 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromSetWhile(<ident, adv., no inner>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromSetWhile(
                            alpha, alphanum, false, null,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },

                    // the token here is irrelevant, but it will contain values as if one char
                    // was read
                    new { Result = ScanResult.NoMatch, Token = new SourceToken(12, 1), Position = 12 },

                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromSetWhile(<ident, adv., inner>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromSetWhile(
                            alpha, alphanum, false, () => test.Check('.', false) ? 2 : 0,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(12, 5), Position = 17 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );


                TestCase(
                    "FromSetWhile(<# comment>)",
                    () =>
                    {
                        SourceToken token;
                        test.SkipToToken();
                        var result = test.FromSetWhile(
                            hash, any, false, null,
                            out token, false
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(18, 18), Position = 18 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromSetWhile(<# comment, multiline>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromSetWhile(
                            hash, any, true, null,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(18, 28), Position = 46 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.FromTokenWhile functions
            public static void TestFromTokenWhile()
            {
                var text = "..12345 0x256 0X126 // one line comment\r\n2nd line";
                var source = new ScannerStringSource(text);
                var test = new ScannerTestClass(source);

                var num = new CharSet();
                num.Add('0', '9');

                var any = new CharSet();
                any.Add('\x1', '\xFFFF');

                TestCase(
                    "FromTokenWhile(<real, not empty while>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromTokenWhile(
                            ".", num, false, null, true,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.NoMatch, Token = new SourceToken(0, 1), Position = 0 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<real>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromTokenWhile(
                            ".", num, false, null, false,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(0, 1), Position = 1 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<real>)",
                    () =>
                    {
                        SourceToken token;
                        var result = test.FromTokenWhile(
                            ".", num, false, null, false,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(1, 6), Position = 7 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<hex>)",
                    () =>
                    {
                        SourceToken token;
                        test.SkipToToken();
                        var result = test.FromTokenWhile(
                            new string[] { "0X", "0x" }, num, false, null, false,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(8, 5), Position = 13 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<hex>)",
                    () =>
                    {
                        SourceToken token;
                        test.SkipToToken();
                        var result = test.FromTokenWhile(
                            new string[] { "0X", "0x" }, num, false, null, false,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(14, 5), Position = 19 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<// comment>)",
                    () =>
                    {
                        SourceToken token;
                        test.SkipToToken();
                        var result = test.FromTokenWhile(
                            "//", any, false, null, false,
                            out token, false
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(20, 19), Position = 20 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                TestCase(
                    "FromTokenWhile(<// comment multi>)",
                    () =>
                    {
                        SourceToken token;
                        test.SkipToToken();
                        var result = test.FromTokenWhile(
                            "//", any, true, null, false,
                            out token
                        );
                        var pos = source.Position;
                        return new { Result = result, Token = token, Position = pos };
                    },
                    new { Result = ScanResult.Match, Token = new SourceToken(20, 29), Position = 49 },
                    (a, b) => a.Result == b.Result && a.Token.Equals(b.Token) && a.Position == b.Position,
                    (v) => $"{{{v.Result}, {v.Token}, {v.Position}}}"
                );

                Console.WriteLine();
            }

            // execute test cases for Scanner.FromTo function
            public static void TestFromTo()
            {
                // TODO
            }
        }

        static void TestScanner()
        {
            ScannerTestClass.TestCharSet();
            ScannerTestClass.TestIsSpace();
            ScannerTestClass.TestIsBreak();
            ScannerTestClass.TestMatch();
            ScannerTestClass.TestHasCharacters();
            ScannerTestClass.TestSkipToToken();
            ScannerTestClass.TestCheck();
            ScannerTestClass.TestCheckAny();
            ScannerTestClass.TestGetCharToken();
            ScannerTestClass.TestCheckCharToken();
            ScannerTestClass.TestFromSetWhile();
            ScannerTestClass.TestFromTokenWhile();
            ScannerTestClass.TestFromTo();
        }

        static void Main(string[] args)
        {
            TestScannerStringSource();
            TestScanner();
        }
    }
}
