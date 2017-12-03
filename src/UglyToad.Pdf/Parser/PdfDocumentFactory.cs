﻿namespace UglyToad.Pdf.Parser
{
    using System;
    using System.IO;
    using Content;
    using ContentStream;
    using Cos;
    using IO;
    using Parts;
    using Parts.CrossReference;
    using Util;

    internal static class PdfDocumentFactory
    {
        public static PdfDocument Open(byte[] fileBytes, ParsingOptions options = null)
        {
            var container = Bootstrapper.GenerateContainer(options?.Logger);

            var isLenientParsing = options?.UseLenientParsing ?? true;

            var reader = new RandomAccessBuffer(fileBytes);

            var document = OpenDocument(reader, container, isLenientParsing);

            return document;
        }

        public static PdfDocument Open(string filename, ParsingOptions options = null)
        {
            if (!File.Exists(filename))
            {
                throw new InvalidOperationException("No file exists at: " + filename);
            }

            return Open(File.ReadAllBytes(filename), options);
        }

        private static PdfDocument OpenDocument(IRandomAccessRead reader, IContainer container, bool isLenientParsing)
        {
            var version = container.Get<FileHeaderParser>().ReadHeader(reader, isLenientParsing);

            var crossReferenceOffset = container.Get<FileTrailerParser>().GetXrefOffset(reader, isLenientParsing);

            var pool = new CosObjectPool();

            var crossReferenceTable = container.Get<FileCrossReferenceTableParser>()
                .Parse(reader, isLenientParsing, crossReferenceOffset, pool);

            var dynamicParser = container.Get<DynamicParser>();
            var bruteForceSearcher = new BruteForceSearcher(reader);
            var resourceContainer = new ResourceContainer();

            var root = ParseTrailer(reader, crossReferenceTable, dynamicParser, bruteForceSearcher, pool,
                isLenientParsing);

            if (!(root is PdfDictionary rootDictionary))
            {
                throw new InvalidOperationException("Expected root dictionary, but got this: " + root);
            }

            // in some pdfs the type value "Catalog" is missing in the root object
            if (isLenientParsing && !rootDictionary.ContainsKey(CosName.TYPE))
            {
                rootDictionary.Set(CosName.TYPE, CosName.CATALOG);
            }

            var caching = new ParsingCachingProviders(pool, bruteForceSearcher, resourceContainer);

            return new PdfDocument(reader, version, crossReferenceTable, container, isLenientParsing, caching, new Catalog(rootDictionary));
        }

        private static CosBase ParseTrailer(IRandomAccessRead reader, CrossReferenceTable crossReferenceTable,
            DynamicParser dynamicParser, BruteForceSearcher bruteForceSearcher, CosObjectPool pool, bool isLenientParsing)
        {
            foreach (var value in crossReferenceTable.Dictionary.Values)
            {
                if (value is CosObject temporaryObject)
                {
                    // Loads these objects into the object pool for access later.
                    dynamicParser.Parse(reader, temporaryObject, pool, crossReferenceTable, bruteForceSearcher,
                        isLenientParsing, false);
                }
            }

            CosObject root = (CosObject)crossReferenceTable.Dictionary.GetItemOrDefault(CosName.ROOT);
            if (root == null)
            {
                throw new InvalidOperationException("Missing root object specification in trailer.");
            }

            var rootObject = dynamicParser.Parse(reader, root, pool, crossReferenceTable, bruteForceSearcher,
                isLenientParsing, false);

            return rootObject;
        }
    }
}
