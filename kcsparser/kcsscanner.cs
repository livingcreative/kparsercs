/*
        KPARSER PROJECT C# VERSION

    Utilities library for parsers programming

    (c) livingcreative, 2017

    https://github.com/livingcreative/kparsercs

    kcsscanner.cs
        Example c# scanner implementation, general example on using
        Scanner class
*/

using KParserCS;
using System.Collections.Generic;

namespace KCSParser
{
    // basic C# tokenizer example
    public class CSScanner : Scanner
    {
        private CharSet all;          // all characters set
        private CharSet numeric;      // numeric [0 - 9] characters set
        private CharSet hexadecimal;  // hexadecimal [0 - 9, A - F, a - f] characters set
        private CharSet alpha;        // alpha characters set (not exact, unicode range needs refinement)
        private CharSet alphanum;     // alpha + numeric characters set
        private string[] hexprefixes; // hexadecimal prefixes
        private string[] escapes;     // all predefined escape sequences
        private string[] compounds;   // all compound sequences

        // construct scanner instance for given source
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

            hexprefixes = new string[]
            {
                "0x", "0X"
            };

            escapes = new string[]
            {
                "\\'", "\\\"", "\\\\",
                "\\t", "\\r", "\\n", "\\b", "\\f", "\\0"
            };

            compounds = new string[]
            {
                "<<=", ">>=",
                 "==", "!=", "=>", "&&", "??", "++", "--", "||", ">=", "<=",
                "+=", "-=", "/=", "*=", "%=", "&=", "|=", "^=", "->"
            };
        }


        // type of token
        public enum TokenType
        {
            Unknown,      // token haven't been scanned yet
            Identifier,   // any valid identifier (including possible keywords)
            Number,       // any integer number, decimal or hexadecimal (might be incomplete)
            RealNumber,   // any real (float or double) number (might be incomplete)
            Character,    // character (single quoted literal, might be malformed)
            String,       // any string (including $ and @ strings, might be incomplete or malformed)
            Comment,      // any comment (single- or multi-line)
            Symbol,       // any standalone character or compiund sequence
            Preprocessor, // preprocessor token (as a whole, not parsed, including possible comments inside)
            Spacer,       // sequence of spaces/line breaks
            Invalid       // invalid token/character
        }

        // basic C# token class
        //      stores type of token and SourceToken value
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


        // get source tokens
        public IEnumerable<Token> DumpTokens(bool includespacers)
        {
            // while end of source isn't reached move to next possible token and
            // scan it
            SourceToken token;
            while (SkipToToken(out token))
            {
                // at this point current source position is at some non spacing
                // character

                // return skipped spaces/line breaks as a token if requested
                if (includespacers && token.Length > 0)
                    yield return new Token(TokenType.Spacer, token);

                yield return ReadToken();
            }

            yield break;
        }


        // read current token
        private Token ReadToken()
        {
            SourceToken token = new SourceToken();
            var type = TokenType.Unknown;
            var c = CharCurrent;

            // here all possible scans could be run in a loop to determine and
            // scan current token, for speeding up this process
            // current source character checked first

            // all scan checks should be performed in particular order for
            // getting correct result, special attention should be made for
            // tokens of different types starting with same character sets

            // here checks are done for most frequent token types first

            // identifier starts with following characters, so it's most
            // high probability to try scan identifier first
            if (c == '_' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' ||
                c >= '\u0100' || c == '\\')
            {
                // try to scan identifier
                if (ScanIdent(out token))
                    type = TokenType.Identifier;
            }
            // next most frequent token type is comment, comments start with /
            // character, so try scan a comment when / encountered
            else if (c == '/')
            {
                if (ScanComment(out token))
                    type = TokenType.Comment;
            }
            // from this character string literal can start
            // try scan string
            else if (c == '"')
            {
                var isstr = ScanString(() => IsEscape(EscapeCheckContext.Character), out token);
                if (Match(isstr))
                    type = TokenType.String;
            }
            // only number could start with digits, try to scan number
            else if (c >= '0' && c <= '9')
            {
                // it is at least some integer number token
                type = TokenType.Number;

                // hexadecimal number literal can't have real part
                if (!ScanHexadecimal(out token))
                {
                    // it's not hexadecimal number - it's integer or real
                    ScanDecimal(out token);

                    // try scan integer postfix, if there's postfix it's integer
                    // number
                    SourceToken tok;
                    if (ScanIntegerPostfix(out tok))
                        token.Length += tok.Length;
                    else
                    {
                        // try to scan "fractional" part of a number
                        if (ScanReal(out tok))
                        {
                            token.Length += tok.Length;
                            type = TokenType.RealNumber;
                        }
                    }
                }
            }
            // from this character interpolated string literal can start
            // try scan interpolated string
            else if (c == '$')
            {
                // interpolated string could be usual double quoted string or
                // @ verbatim string, "eat" $ character and try to scan one of
                // string variants
                GetCharToken(false, null, out token);

                SourceToken tok;
                bool isstring = Match(AnyMatch(
                    out tok,

                    (out SourceToken t) => ScanString(
                        () => InterpolationInnerScan(true, false), out t
                    ),

                    (out SourceToken t) => ScanVerbatimString(
                        () => InterpolationInnerScan(false, true), out t
                    )
                ));

                if (isstring)
                    token.Length += tok.Length;

                type = isstring ? TokenType.String : TokenType.Invalid;
            }
            // from . character real number can start, or it's a single dot
            else if (c == '.')
            {
                if (ScanReal(out token))
                    type = TokenType.RealNumber;
            }
            // from ' character only character literal can start
            else if (c == '\'')
            {
                ScanCharacter(out token);
                type = TokenType.Character;
            }
            // "verbatim" character can start string or @ident
            else if (c == '@')
            {
                if (Match(ScanVerbatimString(null, out token)))
                    type = TokenType.String;
                else
                {
                    GetCharToken(false, null, out token);

                    SourceToken ident;
                    if (!ScanIdent(out ident))
                        type = TokenType.Invalid;
                    else
                    {
                        token.Length += ident.Length;
                        type = TokenType.Identifier;
                    }
                }
            }
            // only preprocessor directive can start with # character
            else if (c == '#')
            {
                ScanPreprocessor(out token);
                type = TokenType.Preprocessor;
            }

            // if none of previous checks detected any kind of token
            // this is symbol or invalid character token, check for it here
            // try to match compounds first, and single characters next
            if (type == TokenType.Unknown)
            {
                bool validsymbol =
                    CheckAny(compounds, out token) ||
                    CheckAny(".();,{}=[]:<>+-*/?%&|^!~", out token);

                if (validsymbol)
                    type = TokenType.Symbol;
                else
                {
                    // all other stuff (unknown/invalid symbols)
                    GetCharToken(false, null, out token);
                    type = TokenType.Invalid;
                }
            }

            return new Token(type, token);
        }


        // kind of escape sequences to check for
        private enum EscapeCheckContext
        {
            Identifier, // check for escape sequences allowed in identifier
            Character   // check for escape sequences allowed in string and character literals
        }

        // detect escape character sequence
        //      unicode escape sequence: \u(hexdigit)
        //          count of digits is not checked here
        //      hexadecimal escape sequence: \x(hexdigit)
        //          count of digits is not checked here
        //      other escape sequences are: \0 \t \b \r \n \f
        private int IsEscape(EscapeCheckContext context)
        {
            SourceToken token;

            var unicodeescape = FromTokenWhile(
                "\\u", hexadecimal, false, null,
                false, out token, false
            );

            if (Match(unicodeescape))
                return token.Length;

            if (context == EscapeCheckContext.Character)
            {
                int length;
                if (CheckAny(escapes, out length, false) != NO_MATCH)
                    return length;

                unicodeescape = FromTokenWhile(
                    "\\x", hexadecimal, false, null,
                    false, out token, false
                );

                if (Match(unicodeescape))
                    return token.Length;
            }

            return 0;
        }

        // scan for possible comment inside interpolation string braces {}
        // unlike usual ScanComment this version consider "multiliness" of comment
        // which depends on inside which string scan is performed (verbatim strings allow
        // multiline comments)
        // and does not advance current position, just returns length
        private int ScanInterpolationComment(bool multiline)
        {
            SourceToken token;
            var result = FromTo("/*", "*/", multiline, null, false, out token, false);

            return Match(result) ? token.Length : 0;
        }

        // detect interpolation string nested code and treat it as a single
        // character inner sequence
        private int InterpolationInnerScan(bool checkcharescape, bool multiline)
        {
            if (checkcharescape)
            {
                var esc = IsEscape(EscapeCheckContext.Character);
                if (esc > 0)
                    return esc;
            }

            SourceToken interp;
            var result = FromTo(
                "{", "}", multiline, () => ScanInterpolationComment(multiline),
                true, out interp, false
            );

            return Match(result) ? interp.Length : 0;
        }


        // try to scan identifier token (does not account for @)
        //      alpha(alphanum)
        //      alpha is [A - Z, a - z, _, <unicode ranges>]
        //      alphanum is [alpha, 0 - 9]
        private bool ScanIdent(out SourceToken token)
        {
            var result = FromSetWhile(
                alpha, alphanum, false, () => IsEscape(EscapeCheckContext.Identifier),
                out token
            );

            return Match(result);
        }

        // try to scan comment token (both // and /* */ types)
        //      single line: // <any> <line end>
        //      multiline:   /* <any> */
        private bool ScanComment(out SourceToken token)
        {
            return Match(AnyMatch(
                out token,
                (out SourceToken t) => FromTokenWhile("//", all, false, null, false, out t),
                (out SourceToken t) => FromTo("/*", "*/", true, null, false, out t)
            ));
        }

        // try to scan string literal (usual or interpolated)
        //      usual string: "<chars and escapes>"
        //      interp. string: $"<chars and escapes>"
        private ScanResult ScanString(InnerScan inner, out SourceToken token)
        {
            return FromTo("\"", "\"", false, inner, false, out token);
        }

        // try to scan verbatim string literal
        //      verbatim string: @"<chars, no escapes, line breaks allowed>"("<chars>")
        private ScanResult ScanVerbatimString(InnerScan inner, out SourceToken token)
        {
            var result = FromTo("@\"", "\"", true, inner, false, out token);

            // continue with contigous double quoted strings only if there was full match
            if (result == ScanResult.Match)
            {
                SourceToken tok;
                ScanResult next;
                while (Match(next = FromTo("\"", "\"", true, inner, false, out tok)))
                {
                    result = next;
                    token.Length += tok.Length;
                }
            }

            return result;
        }

        // try to scan character literal
        //      character literal: '<characters or escapes>'
        //      count of included characters is not checked here
        private bool ScanCharacter(out SourceToken token)
        {
            var result = FromTo(
                "'", "'", false,
                () => IsEscape(EscapeCheckContext.Character),
                false, out token
            );

            return Match(result);
        }

        // try to scan integer number postfix (lLuU)
        private bool ScanIntegerPostfix(out SourceToken token)
        {
            return CheckAny("lLuU", out token);
        }

        // try to scan hexadecimal literal
        //      hexadecimal literal: 0x(hexadecimal)
        //      hexadecimal is [0 - 9, A - F, a - f]
        private bool ScanHexadecimal(out SourceToken token)
        {
            var result = Match(FromTokenWhile(
                hexprefixes, hexadecimal, false, null,
                false, out token
            ));

            SourceToken pf;
            if (result && ScanIntegerPostfix(out pf))
                token.Length += pf.Length;

            return result;
        }

        // try to scan decimal integer literal
        //      decimal(decimal)
        //      decimal is [0 - 9]
        private bool ScanDecimal(out SourceToken token)
        {
            var result = FromSetWhile(numeric, numeric, false, null, out token);
            return Match(result);
        }

        // try to scan decimal real (float or double) literal (fractional part of it)
        //      real fractional: .(decimal)[(eE)(+-)(decimal)]
        private bool ScanReal(out SourceToken token)
        {
            var result = FromTokenWhile(".", numeric, false, null, true, out token);

            if (Match(result))
            {
                // optional E/e part
                if (CheckAny("eE") != NO_MATCH)
                {
                    ++token.Length;

                    // optional +/- after exponent sign
                    if (CheckAny("+-") != NO_MATCH)
                        ++token.Length;

                    // exponent digits
                    SourceToken exp;
                    if (ScanDecimal(out exp))
                        token.Length += exp.Length;
                }

                // optional postfix
                if (CheckAny("fdFD") != NO_MATCH)
                    ++token.Length;
            }

            return Match(result);
        }

        // try to scan preprocessor directive
        //      preprocessor: # <any> <line end>
        private bool ScanPreprocessor(out SourceToken token)
        {
            var result = FromTokenWhile("#", all, false, null, false, out token);
            return Match(result);
        }
    }
}
