/*
        KPARSER PROJECT C# VERSION

    Utilities library for parsers programming

    (c) livingcreative, 2017

    https://github.com/livingcreative/kparsercs

    kscannersource.cs
        IScannerSource interface, abstraction for source text
*/

namespace KParserCS
{
    /// <summary>
    /// Interface for scanner source
    /// </summary>
    /// <remarks>
    /// Scanner treats source text as a "flat" bunch of characters
    /// including line breaks and other possible special characters
    /// </remarks>
    public interface IScannerSource
    {
        /// <summary>
        /// Gets current scanning position inside source text
        /// </summary>
        int Position { get; }

        /// <summary>
        /// Gets length of the whole source text (total number of characters)
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Indicates if source end has been reached
        /// </summary>
        /// <remarks>
        /// Has <c>true</c> value when <c>Position</c> reached end of source <c>(Position == Length)</c>
        /// and <c>false</c> otherwise
        /// </remarks>
        bool IsEnd { get; }

        /// <summary>
        /// Gets source character at current Position
        /// </summary>
        /// <remarks>
        /// Reading at invalid Position is not allowed.
        /// By default, scanner won't read at invalid position
        /// </remarks>
        char CharCurrent { get; }

        /// <summary>
        /// Returns source character at specific offset form current position
        /// </summary>
        /// <param name="offset">Offset relative to current position</param>
        /// <returns>character at given offset</returns>
        /// <remarks>
        /// Reading at invalid position is not allowed.
        /// By default, scanner won't read at invalid position
        /// </remarks>
        char CharAt(int offset);

        /// <summary>
        /// Moves current position by specified advance
        /// </summary>
        /// <param name="advance">Advance to move position by</param>
        /// <remarks>
        /// Both negative and positive advances are allowed.
        /// Moving outside valid source text range is not allowed, except end position.
        /// Scanner won't advance to invalid position except position where
        /// IsEnd will be <c>true</c>
        /// </remarks>
        void Advance(int advance = 1);

        /// <summary>
        /// Converts source token to individual string
        /// </summary>
        /// <param name="token">Token to convert to string</param>
        /// <returns>String which contains characters of given token</returns>
        /// <remarks>
        /// Token must have valid position and length for given source
        /// </remarks>
        string TokenToString(SourceToken token);
    }
}
