using System.Diagnostics;

namespace KParserCS
{
    /// <summary>
    /// Default <c>IScannerSource</c> implementation which stores source inside a string
    /// </summary>
    public class ScannerStringSource : IScannerSource
    {
        private string _source; // string with source text
        private int    _pos;    // current position

        /// <summary>
        /// Initializes new instance with empty source string
        /// </summary>
        public ScannerStringSource() :
            this(string.Empty)
        { }

        /// <summary>
        /// Initializes new instance with given string
        /// </summary>
        /// <param name="source">String with source text</param>
        /// <remarks>
        /// <paramref name="source"/> must not be <c>null</c>
        /// </remarks>
        public ScannerStringSource(string source)
        {
            Debug.Assert(source != null, "Source string must not be null");

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
}
