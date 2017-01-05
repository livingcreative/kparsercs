using System.Collections.Generic;

namespace KParserCS
{
    public class CSScanner : Scanner
    {
        private CharSet all;
        private CharSet numeric;
        private CharSet hexadecimal;
        private CharSet alpha;
        private CharSet alphanum;
        private List<string> escapes;
        private List<string> compounds;

        public CSScanner(IScannerSource source) :
            base(source)
        {
            all = new CharSet();
            all.Add('\u0001', '\uFFFF');

            numeric = new CharSet();
            numeric.Add('0', '9');

            hexadecimal = new CharSet();
            hexadecimal.Add('0', '9');
            hexadecimal.Add('A', 'F');
            hexadecimal.Add('a', 'f');

            alpha = new CharSet();
            alpha.Add('_');
            alpha.Add('A', 'Z');
            alpha.Add('a', 'z');
            // TODO: refine alpha range
            alpha.Add('\u0100', '\uFFFF');

            alphanum = new CharSet(alpha);
            alphanum.Add('0', '9');

            escapes = new List<string>()
            {
                "\\'", "\\\"", "\\\\",
                "\\t", "\\r", "\\n", "\\b", "\\f", "\\0"
            };

            compounds = new List<string>()
            {
                "<<=", ">>=",
                "||", "&&", "==", "!=", ">=", "<=",
                "++", "--", "=>", "??",
                "+=", "-=", "/=", "*=", "%=", "&=", "|=", "^=",
                "->"
            };
        }

        public enum TokenType
        {
            Identifier,
            Number,
            RealNumber,
            Character,
            String,
            Comment,
            Symbol,
            Preprocessor,
            Unknown
        }

        public class Token
        {
            TokenType   _type;
            SourceToken _token;

            public Token(TokenType type, SourceToken token)
            {
                _type = type;
                _token = token;
            }

            public TokenType Type => _type;
            public SourceToken SourceToken => _token;
        }

        public IEnumerable<Token> Tokens
        {
            get
            {
                while (NextCharToken(true))
                {
                    // this is so stupid, no need to construct it here
                    SourceToken token = new SourceToken();

                    var type = TokenType.Unknown;
                    var c = CharCurrent;

                    if (c == '_' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' ||
                        c >= '\u0100' || c == '\\')
                    {
                        // this should be identifier
                        if (ScanIdent(out token))
                            type = TokenType.Identifier;
                    }
                    else if (c == '/')
                    {
                        // comments or standalone character
                        if (ScanComment(out token))
                            type = TokenType.Comment;
                    }
                    else if (c >= '0' && c <= '9')
                    {
                        // this should be some kind number
                        type = TokenType.Number;

                        ScanInteger(out token);

                        SourceToken real;
                        if (ScanReal(out real))
                        {
                            token.Length += real.Length;
                            type = TokenType.RealNumber;
                        }
                    }
                    else if (c == '$' || c == '"')
                    {
                        // string token or standalone $
                        if (ScanString(false, out token))
                            type = TokenType.String;
                    }
                    else if (c == '.')
                    {
                        // real number or dot
                        if (ScanReal(out token))
                            type = TokenType.RealNumber;
                    }
                    else if (c == '\'')
                    {
                        ScanCharacter(out token);
                        type = TokenType.Character;
                    }
                    else if (c == '@')
                    {
                        // this could be identifier or @ string

                        if (ScanString(true, out token))
                            type = TokenType.String;
                        else
                        {
                            GetCharToken(false, null, true, out token);

                            SourceToken ident;
                            if (!ScanIdent(out ident))
                                type = TokenType.Symbol;
                            else
                            {
                                token.Length += ident.Length;
                                type = TokenType.Identifier;
                            }
                        }
                    }
                    else if (c == '#')
                    {
                        ScanPreprocessor(out token);
                        type = TokenType.Preprocessor;
                    }

                    if (type == TokenType.Unknown)
                    {
                        bool validsymbol =
                            CheckAny(compounds, true, out token) ||
                            CheckAny("().;{},=[]+-*/%&|^!~<>?:", true, out token);

                        if (validsymbol)
                            type = TokenType.Symbol;
                        else
                            // all other stuff (unknown/invalid symbols)
                            GetCharToken(false, null, true, out token);
                    }

                    yield return new Token(type, token);
                }

                yield break;
            }
        }

        private enum EscapeCheckContext
        {
            Identifier,
            Character
        }

        private int IsEscape(EscapeCheckContext context)
        {
            SourceToken token;

            if (FromTokenWhile("\\u", hexadecimal, false, null, false, false, out token) == ScanResult.Match)
                return token.Length;

            if (context == EscapeCheckContext.Character)
            {
                foreach (var s in escapes)
                {
                    if (Check(s, false))
                        return s.Length;
                }

                if (FromTokenWhile("\\x", hexadecimal, false, null, false, false, out token) == ScanResult.Match)
                    return token.Length;
            }

            return 0;
        }

        private bool ScanIdent(out SourceToken token)
        {
            var result = FromSetWhile(
                alpha, alphanum, false, () => IsEscape(EscapeCheckContext.Identifier),
                true, out token
            );

            return result == ScanResult.Match;
        }

        private bool ScanComment(out SourceToken token)
        {
            var result = FromTokenWhile("//", all, false, null, true, false, out token);

            if (result == ScanResult.NoMatch)
                result = FromTo("/*", "*/", true, null, true, out token);

            return result != ScanResult.NoMatch;
        }

        private bool ScanString(bool verbatim, out SourceToken token)
        {
            if (verbatim)
                return FromTo("@\"", "\"", true, null, true, out token) != ScanResult.NoMatch;
            else
            {
                var result = FromTo(
                    "\"", "\"", false, () => IsEscape(EscapeCheckContext.Character),
                    true, out token
                );

                if (result == ScanResult.NoMatch)
                    result = FromTo(
                        "$\"", "\"", false, () => IsEscape(EscapeCheckContext.Character),
                        true, out token
                    );

                return result != ScanResult.NoMatch;
            }
        }

        private bool ScanCharacter(out SourceToken token)
        {
            var result = FromTo(
                "'", "'", false, () => IsEscape(EscapeCheckContext.Character),
                true, out token
            );

            return result != ScanResult.NoMatch;
        }

        private bool ScanInteger(out SourceToken token)
        {
            var result =
                FromTokenWhile("0x", hexadecimal, false, null, true, false, out token);

            if (result == ScanResult.NoMatch)
                result = FromTokenWhile("0X", hexadecimal, false, null, true, false, out token);

            if (result == ScanResult.NoMatch)
                result = FromSetWhile(numeric, numeric, false, null, true, out token);

            return result == ScanResult.Match;
        }

        private bool ScanReal(out SourceToken token)
        {
            var result = FromTokenWhile(".", numeric, false, null, true, true, out token);

            if (result == ScanResult.Match)
            {
                // optional E/e part
                if (CheckAny("eE", true) != -1)
                {
                    ++token.Length;

                    // optional +/- after exponent sign
                    if (CheckAny("+-", true) != -1)
                        ++token.Length;

                    // exponent digits
                    SourceToken exp;
                    if (FromSetWhile(numeric, numeric, false, null, true, out exp) == ScanResult.Match)
                        token.Length += exp.Length;
                }

                // optional postfix
                if (CheckAny("fdFD", true) != -1)
                    ++token.Length;
            }

            return result == ScanResult.Match;
        }

        private bool ScanPreprocessor(out SourceToken token)
        {
            var result = FromTokenWhile("#", all, false, null, true, false, out token);
            return result == ScanResult.Match;
        }
    }
}
