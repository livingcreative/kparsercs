using System;
using System.Collections.Generic;
using System.Linq;

namespace KParserCS
{
    // Token
    //      represents token as subrange inside source text
    public struct SourceToken
    {
        // construct token with given position and length
        public SourceToken(int start, int length = 0)
        {
            Start = start;
            Length = length;
        }

        // token position inside source (index of first character)
        public int Start { get; set; }

        // token length (count of characters contained inside this token)
        public int Length { get; set; }
    }


    // IScannerSource
    //      scanner source interface abstracts source for scanner
    //      scanner treats source text as a "flat" bunch of characters
    //      including line breaks and other possible special characters
    public interface IScannerSource
    {
        // current position inside source text
        int Position { get; }
        // length of the whole source text
        int Length { get; }

        // end of source indicator
        //      true when Position reached end of source (Position == Length)
        //      false otherwise
        bool IsEnd { get; }

        // returns source character at current Position
        //      reading at invalid Position is not allowed
        //      Scanner won't read at invalid position
        char CharCurrent { get; }
        // returns source character at specific offset form current position
        //      reading at invalid position is not allowed
        //      Scanner won't read at invalid position
        char CharAt(int offset);

        // moves current Position by specified advance
        //      both negative and positive advances are allowed
        //      moving outside valid source text range is not allowed
        //      Scanner won't asvance to invalid position except position
        //      where IsEnd will return true
        void Advance(int advance = 1);

        // converts source token to individual string
        string TokenToString(SourceToken token);
    }


    // ScannerStringSource
    //      default IScannerSource source implementation
    //      which stores source inside a string
    public class ScannerStringSource : IScannerSource
    {
        private string _source; // string with source text
        private int    _pos;    // current position

        // constructs empty source
        public ScannerStringSource() :
            this(string.Empty)
        { }

        // construct source with provided string contents
        public ScannerStringSource(string source)
        {
            _source = source;
            _pos = 0;
        }

        // following are implementation of IScannerSource interface
        // for description see comments for IScannerSource

        public int Position => _pos;
        public int Length => _source.Length;

        public bool IsEnd => _pos >= _source.Length;

        public char CharCurrent => _source[_pos];
        public char CharAt(int at) => _source[_pos + at];

        public void Advance(int advance) { _pos += advance; }

        public string TokenToString(SourceToken token) =>
            _source.Substring(token.Start, token.Length);
    }


    // Scanner
    //      basic class which provides basic functions for
    //      splitting source on individual tokens
    public class Scanner
    {
        // return type for basic scanning functions
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

        // helper class that represents set of character ranges
        //      its functionality is very basic and does not handle any incosistencies or
        //      edge cases
        //      typical CharSet supposed to consist of several character ranges
        //      e.g. 'A' - 'Z' and 'a' - 'z' represents all valid source alpha characters
        //      this is more compact and fast way to store character set ranges instead of
        //      using HashSet<char>
        protected class CharSet
        {
            private List<Tuple<char, char>> _set;

            // construct empty CharSet
            public CharSet()
            {
                _set = new List<Tuple<char, char>>();
            }

            // construct CharSet as a copy of another CharSet
            public CharSet(CharSet source)
            {
                _set = new List<Tuple<char, char>>(source._set);
            }

            // add a single character to the set
            public void Add(char ch)
            {
                _set.Add(new Tuple<char, char>(ch, ch));
            }

            // add range of characters to the set
            //      "a" value must be <= "b" value
            public void Add(char a, char b)
            {
                _set.Add(new Tuple<char, char>(a, b));
            }

            // checks whether set contains specific character "ch"
            public bool Contains(char ch)
            {
                return _set.Any(v => ch >= v.Item1 && ch <= v.Item2);
            }
        }


        private IScannerSource _source; // scanner source
        private int            _lines;  // source lines counter


        // construct scanner with specified source
        public Scanner(IScannerSource source)
        {
            _source = source;
            _lines = 0;
        }

        // following members might be overriden by specific scanner implementation
        // Scanner class provides only basic implementation

        // checks if current source character is a space character
        //      returns true if current source character is a space character
        //      default implementation treats all characters in the range from 0 to 32
        //      as space characters except line break characters (\r \n)
        //      end of source isn't treated as space character also
        // specific scanner implementation can override this property to treat other
        // characters as space characters
        protected virtual bool IsSpace =>
            !_source.IsEnd &&
            _source.CharCurrent >= 0 && _source.CharCurrent <= ' ' &&
            _source.CharCurrent != '\r' && _source.CharCurrent != '\n';

        // checks if current source character is a line break character or compound
        //      returns non zero value if current source character is a line break
        //      character or compound, value indicates line break sequence length
        //      default implementation treats \r \n single characters and \r\n \n\r
        //      compounds as line breaks
        //      end of source isn't treated as line break
        // specific scanner implementation can override this property to treat other
        // characters or compounds as line breaks
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

        // checks if two strings are equal from scanner's point of view
        // this function is used for character sequences comparison
        //      by default Scanner uses built-in Equals() to compare strings
        // override this to handle comparison in your own way
        protected virtual bool Equals(string a, string b) => a.Equals(b);

        // following members provide basic scanning functionality
        // and supposed to be used as basic blocks for building specific scanner

        // returns token contents as string
        protected string TokenToString(SourceToken token) =>
            _source.TokenToString(token);

        // returns character at current source position
        //      might be useful for quick check before calling any specific scan
        //      functions
        // do not try to read CharCurrent when end of source is reached
        protected char CharCurrent => _source.CharCurrent;

        // checks whether there are at least count characters remaining before source end
        protected bool HasCharacters(int count) =>
            count <= (_source.Length - _source.Position);

        // skips to next character token
        //      nextline - indicates if end of line characters can be skipped
        //      returns true if there's next non space character available for reading
        // if nextline is not true and current character is line break character
        // function returns false
        // skipping through line break characters/sequences increments line counter
        protected bool NextCharToken(bool nextline)
        {
            while (true)
            {
                if (IsSpace)
                {
                    // if current character is space - advance to next one
                    _source.Advance();
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

        // checks if current source character matches specified character
        //      c         - character to check match for
        //      increment - advance current source position in case of match
        //      returns true if current source character matches given character
        protected bool Check(char c, bool increment)
        {
            var result = !_source.IsEnd && _source.CharCurrent == c;

            if (result && increment)
                _source.Advance();

            return result;
        }

        // checks if current source sequence matches specified character sequence
        //      s         - character sequence to check match for
        //      increment - advance current source position in case of match
        //      returns true if current source character sequence matches given sequence
        // empty or null string is not allowed!
        protected bool Check(string s, bool increment)
        {
            var result =
                HasCharacters(s.Length) &&
                Equals(s, _source.TokenToString(new SourceToken(_source.Position, s.Length)));

            if (result && increment)
                _source.Advance(s.Length);

            return result;
        }

        // checks if current source character matches one of provided characters
        //      characters - characters to check match for
        //      increment  - advance current source position in case of match
        //      returns character index of matched character, -1 otherwise
        protected int CheckAny(IEnumerable<char> characters, bool increment)
        {
            int result = 0;

            foreach (var c in characters)
            {
                if (Check(c, increment))
                    return result;
                ++result;
            }

            return -1;
        }

        // checks if current source character matches one of provided characters
        //      characters - characters to check match for
        //      increment  - advance current source position in case of match
        //      token      - resulting matched token
        //      returns true if one of characters matched
        protected bool CheckAny(IEnumerable<char> characters, bool increment, out SourceToken token)
        {
            token = new SourceToken(_source.Position);

            var result = CheckAny(characters, increment) != -1;

            if (result)
                token.Length = 1;

            return result;
        }

        // checks if current source sequence matches one of given character sequences
        //      compounds - sequences to check
        //          shoul be ordered from longest to shortest if
        //          check is performed for compound characters
        //      increment - advance current source position in case of match
        //      length    - length of the matched string
        //      returns index of matched string, -1 otherwise
        // empty or null strings are not allowed!
        protected int CheckAny(IEnumerable<string> compounds, bool increment, out int length)
        {
            int result = 0;
            length = 0;

            foreach (var s in compounds)
            {
                if (Check(s, increment))
                {
                    length = s.Length;
                    return result;
                }
            }

            return -1;
        }

        // checks if current source sequence matches one of given character sequences
        //      compounds - sequences to check
        //          shoul be ordered from longest to shortest if
        //          check is performed for compound characters
        //      increment - advance current source position in case of match
        //      token     - resulting matched token
        //      returns true if match was found
        // empty or null strings are not allowed!
        protected bool CheckAny(IEnumerable<string> compounds, bool increment, out SourceToken token)
        {
            token = new SourceToken(_source.Position);
            int length = 0;

            var result = CheckAny(compounds, increment, out length) != -1;

            if (result)
                token.Length = length;

            return result;
        }

        // get current character token
        //      nextline - indicates if returning of line end sequence is allowed
        //      escapes  - optional function to check for compound escape char sequences
        //          should return length of found escape sequence or 0 if none found
        //      token    - resulting token at current source position
        //      increment - advance current source position in case of match
        //      returns true if token can be read at current position with given options
        // if nextline is false and current position is at the end of line function
        // returns false
        // if current position is at the end of line and nextline is true whole line
        // break sequence is returned as a token
        protected bool GetCharToken(bool nextline, Func<int> escapes, bool increment, out SourceToken token)
        {
            token = new SourceToken(_source.Position);

            bool result = false;
            var len = IsBreak;

            // if current position isn't at line end
            if (len == 0)
            {
                // check for possible escape character, if requested
                if (escapes != null)
                    len = escapes();

                // no escape found and end of source reached - return false
                if (len == 0 && _source.IsEnd)
                    return false;

                // return single character token, otherwise return token with corresponding
                // length
                token.Length = len == 0 ? 1 : len;

                result = true;
            }
            else
            {
                // if reached here - line break was found, if skip to next line is allowed
                // increment lines counter and return line break sequence as a token
                if (nextline)
                {
                    token.Length = len;
                    ++_lines;
                }
                result = nextline;
            }

            if (result && increment)
                _source.Advance(token.Length);

            return result;
        }

        // matches token given by starting character set and allowed character set
        //      from      - char set which indicates allowed starting token characters
        //      whileset  - char set of characters which allowed to be included in token
        //      nextline  - indicates whether token is allowed to span for multiple lines
        //      escapes   - optional function to check for compound escape char sequences
        //          should return length of found escape sequence or 0 if none found
        //      increment - indicates if current source position should be incremented by
        //          the length of found token
        //      token     - resulting token
        //      returns scan result
        // this function does not return partial matches
        // it scans for tokens which start from specific character set and continues with
        // "whileset" character set
        // could be used for scanning simple tokens like identifiers or numbers
        //      number example:     FromSetWhile([0 - 9], [0 - 9], ...)
        //      identifier example: FromSetWhile([A - Z, a - z, _], [A - Z, a - z, _, 0 - 9], ...)
        protected ScanResult FromSetWhile(CharSet from, CharSet whileset, bool nextline, Func<int> escapes, bool increment, out SourceToken token)
        {
            token = new SourceToken();

            // check match for first allowed character
            SourceToken cs;
            if (GetCharToken(nextline, escapes, false, out cs) &&
                (cs.Length > 1 || from.Contains(_source.CharCurrent)))
                _source.Advance(cs.Length);
            else
                return ScanResult.NoMatch;

            // continue while characters match "whileset"
            token = cs;
            while (GetCharToken(nextline, escapes, false, out cs) &&
                   (cs.Length > 1 || whileset.Contains(_source.CharCurrent)))
            {
                _source.Advance(cs.Length);
                ++token.Length;
            }

            // if increment wasn't requeseted - advance back (to position which was before this call)
            if (!increment)
                _source.Advance(-token.Length);

            return ScanResult.Match;
        }

        // matches token given by starting character sequence and allowed character set
        //      from      - character sequence with which token starts
        //          empty or null string is not allowed!
        //      whileset  - char set of characters which allowed to be included in token
        //      nextline  - indicates whether token is allowed to span for multiple lines
        //      escapes   - optional function to check for compound escape char sequences
        //          should return length of found escape sequence or 0 if none found
        //      increment - indicates if current source position should be incremented by
        //          the length of found token
        //      token     - resulting token
        //      returns scan result
        // this function does not return partial matches
        // it scans for tokens which start from specific character sequence and continues
        // with "whileset" character set
        // could be used for scanning simple tokens which start from specific sequence
        //      hex number example: FromTokenWhile("0x", [0 - 9, A - F, a - f], ...)
        protected ScanResult FromTokenWhile(string from, CharSet whileset, bool nextline, Func<int> escapes, bool increment, bool notemptywhile, out SourceToken token)
        {
            token = new SourceToken(_source.Position);

            // check "from" sequence match
            if (!Check(from, true))
                return ScanResult.NoMatch;

            token.Length += from.Length;

            // continue while characters match "whileset"
            SourceToken cs;
            while (GetCharToken(nextline, escapes, false, out cs) &&
                   (cs.Length > 1 || whileset.Contains(_source.CharCurrent)))
            {
                _source.Advance(cs.Length);
                ++token.Length;
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

        // matches token given by starting and ending character sequences
        //      fromtoken - character sequence with which token starts
        //          empty or null string is not allowed!
        //      totoken   - character sequence with which token ends
        //          empty or null string is not allowed!
        //      nextline  - indicates whether token is allowed to span for multiple lines
        //      escapes   - optional function to check for compound escape char sequences
        //          should return length of found escape sequence or 0 if none found
        //      increment - indicates if current source position should be incremented by
        //          the length of found token
        //      token     - resulting token
        //      returns scan result
        // this function might return partial match
        // could be used for scanning tokens contained within paired character sequences
        //      C-style comment example: FromTo("/*", "*/", ...)
        protected ScanResult FromTo(string fromtoken, string totoken, bool nextline, Func<int> escapes, bool increment, out SourceToken token)
        {
            token = new SourceToken(_source.Position);

            // check "from" sequence match
            if (!Check(fromtoken, true))
                return ScanResult.NoMatch;

            token.Length += fromtoken.Length;

            var result = ScanResult.NoMatch;

            // continue with characters up until match of "totoken"
            SourceToken cs;
            while (GetCharToken(nextline, escapes, false, out cs))
            {
                if (Check(totoken, true))
                {
                    token.Length += totoken.Length;
                    result = ScanResult.Match;
                    break;
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
