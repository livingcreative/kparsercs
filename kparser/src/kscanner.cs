/*
        KPARSER PROJECT C# VERSION

    Utilities library for parsers programming

    (c) livingcreative, 2017

    https://github.com/livingcreative/kparsercs

    kscanner.cs
        Scanner base class for building lexers/tokenizers
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KParserCS
{
    /// <summary>
    /// Provides basic functions for splitting source on individual tokens
    /// </summary>
    public class Scanner
    {
        /// <summary>
        /// Return type for basic scanning functions
        /// </summary>
        protected enum ScanResult
        {
            // scanning not matched expected token, current position remains as before
            // token scan attempt
            NoMatch,

            // scanning fully matched expected token, current position adjusted
            // with respect to "increment" parameter
            Match,

            // scanning partially matched expected token and was terminated by
            // end of line (expected token was not supposed to be multi-line token),
            // current position adjusted with respect to "increment" parameter
            MatchTrimmedEOL,

            // scanning partially matched expected token and was terminated by
            // end of source (source unexpectedly ended),
            // current position adjusted with respect to "increment" parameter
            MatchTrimmedEOF
        }

        /// <summary>
        /// Helper class that represents set of character ranges
        /// </summary>
        /// <remarks>
        /// Its functionality is very basic and does not handle any incosistencies or
        /// edge cases.
        /// Typical CharSet supposed to consist of several character ranges.
        /// E.g. 'A' - 'Z' and 'a' - 'z' represents all valid source alpha characters.
        /// This is more compact and fast way to store character set ranges instead of
        /// using HashSet<char>.
        /// </remarks>
        protected class CharSet
        {
            private List<Tuple<char, char>> _set;

            /// <summary>
            /// Initializes new instance with empty set
            /// </summary>
            public CharSet()
            {
                _set = new List<Tuple<char, char>>();
            }

            /// <summary>
            /// Initializes new instance as a copy of existing CharSet object
            /// </summary>
            /// <param name="source">Source object which will be copied into new one</param>
            public CharSet(CharSet source)
            {
                _set = new List<Tuple<char, char>>(source._set);
            }

            /// <summary>
            /// Adds a single character to the set
            /// </summary>
            /// <param name="ch">Character to add</param>
            /// <remarks>No checks will be performed on add</remarks>
            public void Add(char ch)
            {
                _set.Add(new Tuple<char, char>(ch, ch));
            }

            /// <summary>
            /// Adds range of characters to the set
            /// </summary>
            /// <param name="a">First character of a range (inclusive)</param>
            /// <param name="b">Last character of a range (inclusive)</param>
            /// <remarks>
            /// <paramref name="a"/> value must be less than or equal to
            /// <paramref name="b"/> value.
            /// No checks will be performed on add
            /// </remarks>
            public void Add(char a, char b)
            {
                _set.Add(new Tuple<char, char>(a, b));
            }

            /// <summary>
            /// Checks whether set contains specific character
            /// </summary>
            /// <param name="ch">Character to check</param>
            /// <returns><c>true</c>if set contains given character</returns>
            public bool Contains(char ch)
            {
                return _set.Any(v => ch >= v.Item1 && ch <= v.Item2);
            }
        }


        private IScannerSource _source; // scanner source
        private int            _lines;  // source lines counter


        /// <summary>
        /// Initializes new instance of a scanner object with given source
        /// </summary>
        /// <param name="source">Source for scanning</param>
        /// <remarks>
        /// <paramref name="source"/> must not be null
        /// </remarks>
        protected Scanner(IScannerSource source)
        {
            Debug.Assert(source != null, "Scanner source must not be null");

            _source = source;
            _lines = 0;
        }

        /// <summary>
        /// Inner scan delegate type
        /// </summary>
        /// <returns>
        /// Returns count of characters matched for any inner sequence
        /// at current position
        /// </returns>
        /// <remarks>
        /// Can be used to read escape sequences and some inner elements of containing
        /// sequence
        /// </remarks>
        protected delegate int InnerScan();

        /// <summary>
        /// Scan callback delegate for AnyMatch utility function
        /// </summary>
        /// <param name="token">Resulting token on successful scan</param>
        /// <returns>Returns match result</returns>
        protected delegate ScanResult ScanCallback(out SourceToken token);

        // following members might be overriden by specific scanner implementation
        // Scanner class provides only basic implementation

        /// <summary>
        /// Checks if current source character is a space character
        /// </summary>
        /// <remarks>
        /// Returns <c>true</c> if current source character is a space character.
        /// Default implementation treats all characters in the range from 0 to 32
        /// as space characters except line break characters (\r \n).
        /// End of source isn't treated as space character also.
        /// Specific scanner implementation can override this property to treat other
        /// characters as space characters.
        /// Space characters are always single characters, they can not be sequences.
        /// </remarks>
        protected virtual bool IsSpace
        {
            get
            {
                return
                    !_source.IsEnd &&
                    _source.CharCurrent >= 0 && _source.CharCurrent <= ' ' &&
                    _source.CharCurrent != '\r' && _source.CharCurrent != '\n';
            }
        }

        /// <summary>
        /// Checks if current source character is a line break character or sequence
        /// </summary>
        /// <remarks>
        /// Returns non zero value if current source character is a line break
        /// character or sequence, value indicates line break sequence length.
        /// Default implementation treats \r \n single characters and \r\n \n\r
        /// sequences as line breaks.
        /// End of source isn't treated as line break.
        /// Specific scanner implementation can override this property to treat other
        /// characters or sequences as line breaks.
        /// </remarks>
        protected virtual int IsBreak
        {
            get
            {
                // if end of source reached - return 0 (no line break found)
                if (_source.IsEnd)
                    return 0;

                int result = 0;

                // check if current character is one of line break characters (\r or \n)
                if (_source.CharCurrent == '\n' || _source.CharCurrent == '\r')
                {
                    ++result;

                    // check for possible \r\n or \n\r sequence and treat it as one
                    // compound line break sequence
                    bool seq =
                        HasCharacters(2) &&
                        (_source.CharCurrent == '\n' && _source.CharAt(1) == '\r' ||
                         _source.CharCurrent == '\r' && _source.CharAt(1) == '\n');

                    if (seq)
                        ++result;
                }

                return result;
            }
        }

        // following members provide basic scanning functionality
        // and supposed to be used as basic blocks for building specific scanner

        /// <summary>
        /// Helper function to check if scan matched
        /// </summary>
        /// <param name="result"><c>ScanResult</c> value to check</param>
        /// <returns>Returns <c>true</c> for eny result, but <c>ScanResult.NoMatch</c></returns>
        protected static bool Match(ScanResult result) => result != ScanResult.NoMatch;

        /// <summary>
        /// Helper function to check if scan not matched
        /// </summary>
        /// <param name="result"><c>ScanResult</c> value to check</param>
        /// <returns>Returns <c>false</c> for eny result, but <c>ScanResult.Match</c></returns>
        protected static bool NotMatch(ScanResult result) => result == ScanResult.NoMatch;

        /// <summary>
        /// Helper function to chain matching alternatives
        /// </summary>
        /// <param name="token">Resulting matched token in case of any match</param>
        /// <param name="scans">Set of scanning callback functions</param>
        /// <returns>
        /// Returns match result of first matched callback or <c>ScanResult.NoMatch</c>
        /// in case of no match
        /// </returns>
        protected static ScanResult AnyMatch(out SourceToken token, params ScanCallback[] scans)
        {
            foreach (var scan in scans)
            {
                var match = scan(out token);
                if (Match(match))
                    return match;
            }

            // NOTE: actually assignment of emty token is not needed here, but
            //       c# insists, change to ref also will force caller to initialize
            //       passed token
            token = new SourceToken();
            return ScanResult.NoMatch;
        }

        /// <summary>
        /// Converts source token to individual string
        /// </summary>
        /// <param name="token">Token to convert to string</param>
        /// <returns>String which contains characters of given token</returns>
        /// <remarks>
        /// Token must have valid position and length for given source
        /// </remarks>
        protected string TokenToString(SourceToken token) =>
            _source.TokenToString(token);

        /// <summary>
        /// Gets character at current source position
        /// </summary>
        /// <remarks>
        /// Might be useful for quick check before calling any specific scan functions.
        /// Do not try to read CharCurrent when end of source is reached.
        /// </remarks>
        protected char CharCurrent => _source.CharCurrent;

        /// <summary>
        /// Gets processed lines count during scan
        /// </summary>
        /// <remarks>
        /// Every encountered line break character/sequence increments this number
        /// </remarks>
        protected int LineCount => _lines;

        /// <summary>
        /// Checks whether there are at least <c>count</c> characters remaining before source end
        /// </summary>
        /// <param name="count">For how many characters to check</param>
        /// <returns>Returns <c>true</c> if <c>count</c> characters can be read before end of source</returns>
        protected bool HasCharacters(int count) =>
            count <= (_source.Length - _source.Position);

        /// <summary>
        /// Skips to next non space or line break token
        /// </summary>
        /// <param name="token">Token which contains skipped portion of source text</param>
        /// <param name="nextline">Indicates that EOL sequences should be skipped</param>
        /// <returns>
        /// Returns <c>true</c> if current position now is at valid non space or
        /// EOL character, <c>false</c> if EOL or end of source reached
        /// </returns>
        /// <remarks>
        /// Skipping through line break characters/sequences increments line counter
        /// </remarks>
        protected bool SkipToToken(out SourceToken token, bool nextline = true)
        {
            token = new SourceToken(_source.Position);

            while (true)
            {
                if (IsSpace)
                {
                    // if current character is space - advance to next one
                    _source.Advance();
                    ++token.Length;
                }
                else
                {
                    // check for line break character/sequence
                    var br = IsBreak;

                    // if no line break found next character is available if
                    // end of source isn't reached yet
                    if (br == 0)
                        return !_source.IsEnd;


                    if (nextline)
                    {
                        // skipping through multiple lines is allowed, skip current
                        // line break sequence and increment lines counter
                        _source.Advance(br);
                        token.Length += br;
                        ++_lines;
                    }
                    else
                    {
                        // current position is at line end and skipping through lines is
                        // not allowed, return false
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Skips to next non space or line break token
        /// </summary>
        /// <param name="nextline">Indicates that EOL sequences should be skipped</param>
        /// <returns>
        /// Returns <c>true</c> if current position now is at valid non space or
        /// EOL character, <c>false</c> if EOL or end of source reached
        /// </returns>
        /// <remarks>
        /// Skipping through line break characters/sequences increments line counter
        /// </remarks>
        protected bool SkipToToken(bool nextline = true)
        {
            SourceToken token;
            return SkipToToken(out token, nextline);
        }

        /// <summary>
        /// Checks if current source character matches specified character
        /// </summary>
        /// <param name="c">The character to check match for</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> in case of match</returns>
        protected bool Check(char c, bool increment = true)
        {
            var result = !_source.IsEnd && _source.CharCurrent == c;

            if (result && increment)
                _source.Advance();

            return result;
        }

        /// <summary>
        /// Checks if current source sequence matches specified character sequence
        /// </summary>
        /// <param name="s">Sequence of characters to check match for</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> if current source character sequence matches given sequence</returns>
        /// <remarks>
        /// Empty or null string is not allowed as an input
        /// </remarks>
        protected bool Check(string s, bool increment = true)
        {
            Debug.Assert(!string.IsNullOrEmpty(s), "string must not be null or empty");

            var result =
                HasCharacters(s.Length) &&
                s.Equals(_source.TokenToString(new SourceToken(_source.Position, s.Length)));

            if (result && increment)
                _source.Advance(s.Length);

            return result;
        }

        // special value for no matched CheckAny result
        protected const int NO_MATCH = -1;

        /// <summary>
        /// Checks if current source character matches one of provided characters
        /// </summary>
        /// <param name="characters">Characters to check match for</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns index of matched character in case of match or <c>NO_MATCH</c> otherwise</returns>
        /// <remarks>
        /// <paramref name="characters"/> must not be <c>null</c>
        /// </remarks>
        protected int CheckAny(IEnumerable<char> characters, bool increment = true)
        {
            Debug.Assert(characters != null, "character source must not be null");

            int result = 0;

            foreach (var c in characters)
            {
                if (Check(c, increment))
                    return result;
                ++result;
            }

            return NO_MATCH;
        }

        /// <summary>
        /// Checks if current source character matches one of provided characters
        /// </summary>
        /// <param name="characters">Characters to check match for</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> in case of match</returns>
        /// <remarks>
        /// <paramref name="characters"/> must not be <c>null</c>
        /// </remarks>
        protected bool CheckAny(IEnumerable<char> characters, out SourceToken token, bool increment = true)
        {
            Debug.Assert(characters != null, "character source must not be null");

            var pos = _source.Position;
            var result = CheckAny(characters, increment) != NO_MATCH;

            token = new SourceToken(pos, result ? 1 : 0);

            return result;
        }

        /// <summary>
        /// Checks if current source sequence matches one of given character sequences
        /// </summary>
        /// <param name="compounds">Sequences to check match for</param>
        /// <param name="length">Length of matched sequence will be stored here</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns index of matched sequence in case of match or <c>NO_MATCH</c> otherwise</returns>
        /// <remarks>
        /// <paramref name="compounds"/> must not be <c>null</c>, empty or null strings are not allowed
        /// </remarks>
        protected int CheckAny(IEnumerable<string> compounds, out int length, bool increment = true)
        {
            Debug.Assert(compounds != null, "compounds source must not be null");

            int result = 0;
            length = 0;

            foreach (var s in compounds)
            {
                if (Check(s, increment))
                {
                    length = s.Length;
                    return result;
                }
                ++result;
            }

            return NO_MATCH;
        }

        /// <summary>
        /// Checks if current source sequence matches one of given character sequences
        /// </summary>
        /// <param name="compounds">Sequences to check match for</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> in case of match</returns>
        /// <remarks>
        /// <paramref name="compounds"/> must not be <c>null</c>, empty or null strings are not allowed
        /// </remarks>
        protected bool CheckAny(IEnumerable<string> compounds, out SourceToken token, bool increment = true)
        {
            Debug.Assert(compounds != null, "compounds source must not be null");

            var length = 0;
            var pos = _source.Position;

            var result = CheckAny(compounds, out length, increment) != NO_MATCH;

            token = new SourceToken(pos, length);

            return result;
        }

        /// <summary>
        /// Gets current character token
        /// </summary>
        /// <param name="nextline">Indicates if returning of line end sequence is allowed</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> in case of match</returns>
        protected bool GetCharToken(bool nextline, InnerScan inner, out SourceToken token, bool increment = true)
        {
            var result = false;
            var len = IsBreak;

            // if current position isn't at line end
            if (len == 0)
            {
                // check for possible inner sequences, if requested
                if (inner != null)
                    len = inner();

                // no inner sequence found and end of source reached - return false
                if (len == 0 && _source.IsEnd)
                {
                    // NOTE: just to c# compiler stop complaining
                    token = new SourceToken();
                    return false;
                }

                // return single character token, otherwise return token with corresponding
                // length
                token = new SourceToken(_source.Position, len == 0 ? 1 : len);

                result = true;
            }
            else
            {
                token = new SourceToken(_source.Position, nextline ? len : 0);

                // if reached here - line break was found, if skip to next line is allowed
                // increment lines counter and return line break sequence as a token
                if (nextline)
                    ++_lines;

                result = nextline;
            }

            if (result && increment)
                _source.Advance(token.Length);

            return result;
        }

        /// <summary>
        /// Gets current character token and matches it agains provided set
        /// </summary>
        /// <param name="set">Character set to check current char belongs to</param>
        /// <param name="nextline">Indicates if returning of line end sequence is allowed</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns <c>true</c> in case of match</returns>
        /// <remarks>
        /// <paramref name="set"/> must not be <c>null</c>.
        /// If character doesn't match given set and it's not any inner sequence
        /// function returns <c>false</c>
        /// </remarks>
        protected bool CheckCharToken(CharSet set, bool nextline, InnerScan inner, out SourceToken token, bool increment = true)
        {
            Debug.Assert(set != null, "set must not be null");

            var result =
                GetCharToken(nextline, inner, out token, false) &&
                (token.Length > 1 || set.Contains(_source.CharCurrent));

            if (result && increment)
                _source.Advance(token.Length);

            return result;
        }

        /// <summary>
        /// Matches token given by starting character set and allowed character set
        /// </summary>
        /// <param name="from">CharSet which holds allowed starting token characters</param>
        /// <param name="whileset">CharSet which holds allowed characters to be included in token after starting one</param>
        /// <param name="multiline">Indicates whether token is allowed to span for multiple lines</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns match result</returns>
        /// <remarks>
        /// <paramref name="from"/> and <paramref name="whileset"/> must not be <c>null</c>.
        /// This function does not return partial matches (it's not possible) in this case.
        /// </remarks>
        protected ScanResult FromSetWhile(CharSet from, CharSet whileset, bool multiline, InnerScan inner, out SourceToken token, bool increment = true)
        {
            Debug.Assert(from != null, "from set must not be null");
            Debug.Assert(whileset != null, "whileset set must not be null");

            // check match for first allowed character
            if (!CheckCharToken(from, multiline, inner, out token))
                return ScanResult.NoMatch;

            // continue while characters match "whileset"
            SourceToken cs;
            while (CheckCharToken(whileset, multiline, inner, out cs))
            {
                token.Length += cs.Length;
            }

            // if increment wasn't requeseted - advance back (to position which was before this call)
            if (!increment)
                _source.Advance(-token.Length);

            return ScanResult.Match;
        }

        /// <summary>
        /// Matches token given by starting character sequence and allowed character set
        /// </summary>
        /// <param name="from">Character sequence with which token starts</param>
        /// <param name="whileset">CharSet which holds allowed characters to be included in token after starting one</param>
        /// <param name="multiline">Indicates whether token is allowed to span for multiple lines</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="notemptywhile">Indicates that match should be returned only if there is at leas one match from while set</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns match result</returns>
        /// <remarks>
        /// <paramref name="from"/> must not be <c>null</c> or empty string.
        /// <paramref name="whileset"/> must not be <c>null</c>.
        /// This function does not return partial matches (it's not possible) in this case.
        /// </remarks>
        protected ScanResult FromTokenWhile(string from, CharSet whileset, bool multiline, InnerScan inner, bool notemptywhile, out SourceToken token, bool increment = true)
        {
            Debug.Assert(!string.IsNullOrEmpty(from), "from string must not be null or empty");
            Debug.Assert(whileset != null, "whileset set must not be null");

            token = new SourceToken(_source.Position);

            // check "from" sequence match
            if (!Check(from))
                return ScanResult.NoMatch;

            token.Length += from.Length;

            // continue while characters match "whileset"
            SourceToken cs;
            while (CheckCharToken(whileset, multiline, inner, out cs))
            {
                token.Length += cs.Length;
            }

            // if notemptywhile requested total length should be more than from length
            if (notemptywhile && token.Length <= from.Length)
            {
                _source.Advance(-token.Length);
                return ScanResult.NoMatch;
            }

            // if increment wasn't requeseted - advance back (to position which was before this call)
            if (!increment)
                _source.Advance(-token.Length);

            return ScanResult.Match;
        }

        /// <summary>
        /// Matches token given by possible starting character sequences and allowed character set
        /// </summary>
        /// <param name="from">Character sequences with which token starts</param>
        /// <param name="whileset">CharSet which holds allowed characters to be included in token after starting one</param>
        /// <param name="multiline">Indicates whether token is allowed to span for multiple lines</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="notemptywhile">Indicates that match should be returned only if there is at leas one match from while set</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns match result</returns>
        /// <remarks>
        /// <paramref name="from"/> must not be <c>null</c> and must not contain <c>null</c> or empty string.
        /// <paramref name="whileset"/> must not be <c>null</c>.
        /// This function does not return partial matches (it's not possible) in this case.
        /// </remarks>
        protected ScanResult FromTokenWhile(IEnumerable<string> from, CharSet whileset, bool multiline, InnerScan inner, bool notemptywhile, out SourceToken token, bool increment = true)
        {
            Debug.Assert(from != null, "from source must not be null");
            Debug.Assert(whileset != null, "whileset set must not be null");

            var result = ScanResult.NoMatch;

            foreach (var tok in from)
            {
                result = FromTokenWhile(
                    tok, whileset, multiline, inner,
                    notemptywhile, out token, increment
                );

                if (result != ScanResult.NoMatch)
                    return result;
            }

            token = new SourceToken();

            return result;
        }

        /// <summary>
        /// Matches token given by starting and ending character sequences
        /// </summary>
        /// <param name="fromtoken">Character sequence with which token starts</param>
        /// <param name="totoken">Character sequence with which token ends</param>
        /// <param name="multiline">Indicates whether token is allowed to span for multiple lines</param>
        /// <param name="inner">Optional function to check for inner sequences which will be treated as compound character token</param>
        /// <param name="allownesting">Indicates if nesting of starting/ending sequences is allowed</param>
        /// <param name="token">Resulting token in case of match</param>
        /// <param name="increment">Indicates if current source position needs to be advanced in case of match</param>
        /// <returns>Returns match result</returns>
        /// <remarks>
        /// <paramref name="fromtoken"/> must not be <c>null</c> or empty string.
        /// <paramref name="totoken"/> must not be <c>null</c> or empty string.
        /// This function might return partial match.
        /// </remarks>
        protected ScanResult FromTo(string fromtoken, string totoken, bool multiline, InnerScan inner, bool allownesting, out SourceToken token, bool increment = true)
        {
            Debug.Assert(!string.IsNullOrEmpty(fromtoken), "fromtoken string must not be null or empty");
            Debug.Assert(!string.IsNullOrEmpty(totoken), "totoken string must not be null or empty");

            token = new SourceToken(_source.Position);

            // check "from" sequence match
            if (!Check(fromtoken))
                return ScanResult.NoMatch;

            int nestinglevel = 1;

            token.Length += fromtoken.Length;

            var result = ScanResult.NoMatch;

            // continue with characters up until match of "totoken"
            SourceToken cs;
            while (GetCharToken(multiline, inner, out cs, false))
            {
                if (allownesting && Check(fromtoken))
                {
                    ++nestinglevel;
                    token.Length += fromtoken.Length;
                    continue;
                }

                if (Check(totoken))
                {
                    --nestinglevel;
                    token.Length += totoken.Length;

                    if (nestinglevel == 0)
                    {
                        result = ScanResult.Match;
                        break;
                    }

                    continue;
                }

                _source.Advance(cs.Length);
                token.Length += cs.Length;
            }

            // check if token end matches ending sequence
            if (result != ScanResult.Match)
            {
                if (_source.IsEnd)
                    result = ScanResult.MatchTrimmedEOF;
                else
                    result = ScanResult.MatchTrimmedEOL;
            }

            // if increment wasn't requeseted - advance back (to position which was before this call)
            if (!increment)
                _source.Advance(-token.Length);

            return result;
        }
    }
}
