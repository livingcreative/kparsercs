using System.Diagnostics;

namespace KParserCS
{
    /// <summary>
    /// Represents a token as subrange inside source text
    /// </summary>
    public struct SourceToken
    {
        /// <summary>
        /// Initializes new token instance with given <c>start</c> and <c>length</c>
        /// </summary>
        /// <param name="start">Position in source text where token starts</param>
        /// <param name="length">Length of the token</param>
        /// <remarks>
        /// Both <paramref name="start"/> and <paramref name="length"/> must be
        /// equal or greater than 0
        /// </remarks>
        public SourceToken(int start, int length = 0)
        {
            Debug.Assert(start >= 0, "SourceToken.Start must be >= 0");
            Debug.Assert(length >= 0, "SourceToken.Length must be >= 0");

            Start = start;
            Length = length;
        }

        /// <summary>
        /// Gets token position inside source (index of first character)
        /// </summary>
        public int Start { get; private set; }

        /// <summary>
        /// Gets or sets token length
        /// (count of characters contained inside this token)
        /// </summary>
        public int Length { get; set; }

        public override string ToString() => $"{Start}:{Length}";
    }
}
