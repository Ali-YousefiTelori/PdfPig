﻿namespace UglyToad.Pdf.Fonts.CidFonts
{
    using Cmap;
    using Cos;

    /// <inheritdoc/>
    /// <summary>
    /// Type 0 CID fonts contain glyph descriptions based on the
    /// Adobe Type 1 font format.
    /// </summary>
    internal class Type0CidFont : ICidFont
    {
        public CosName Type { get; }
        public CosName SubType { get; }
        public CosName BaseFont { get; }
        public CharacterIdentifierSystemInfo SystemInfo { get; }
    }
}
