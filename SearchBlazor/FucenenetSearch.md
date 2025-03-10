# Lucene.Net In-Memory Search Engine

This project implements a full-text search engine using Lucene.Net with an in-memory index. It provides fast search capabilities over a skill database with autocomplete functionality.

## 🔍 Overview

Lucene.Net is a powerful .NET port of the popular Apache Lucene search library. This implementation uses a RAM-based index for optimal performance, making it suitable for applications where search speed is critical and the dataset can fit in memory.

## ⚡ Core Concepts

| Term                        | Description                            | Implementation                                     |
| --------------------------- | -------------------------------------- | -------------------------------------------------- |
| **LuceneVersion**           | Specifies Lucene compatibility version | Using `LuceneVersion.LUCENE_48` (4.8.0)            |
| **Analyzer**                | Tokenizes text and applies filters     | `StandardAnalyzer` for English text analysis       |
| **Directory**               | Index storage location                 | `RAMDirectory` for in-memory storage               |
| **IndexWriter**             | Writes documents to the index          | Adds each Skill as a document with fields          |
| **Document**                | Single record in the index             | Each skill becomes a Document                      |
| **Field**                   | Data element within a Document         | Name, Category, Group, Dependencies, RelatedSkills |
| **TextField**               | Searchable text field                  | Used for all searchable content                    |
| **IndexSearcher**           | Performs searches on the index         | Used in the `Search()` method                      |
| **QueryParser**             | Parses search strings into queries     | `MultiFieldQueryParser` for cross-field searching  |
| **AnalyzingInfixSuggester** | Provides autocomplete functionality    | Powers the `SearchAhead()` method                  |

## 🧠 Application Flow

1. **Data Loading**

   - `LoadDataFromJson()` reads and deserializes the skills database

2. **Indexing**

   - `Index()` creates the Lucene index in RAM
   - Each skill becomes a Document with appropriate fields
   - Field values are processed by the analyzer

3. **Searching**

   - `Search(string input, int page)` executes searches
   - Input is sanitized via `EscapeSearchTerm()`
   - Results are paginated for better UX

4. **Autocomplete**

   - `SearchAhead(string input)` provides real-time suggestions
   - Uses `AnalyzingInfixSuggester` to generate up to 9 options

5. **Cleanup**
   - `Dispose()` releases resources when finished

## 🚀 Dependencies

- **Lucene.Net**: Core search library
- **Lucene.Net.Analysis.Common**: Text analysis tools
- **Lucene.Net.QueryParser**: Query parsing functionality
- **Lucene.Net.Suggest**: Autocomplete features
- **Lucene.Net.Facet**: (Referenced but unused in current implementation)

## 📋 Getting Started

```csharp
// Create search engine instance
using (var searchEngine = new SearchEngine())
{
    // Load and index data
    searchEngine.LoadDataFromJson("skills_database.json");
    searchEngine.Index();

    // Perform a search
    var results = searchEngine.Search("programming", 1);

    // Get autocomplete suggestions
    var suggestions = searchEngine.SearchAhead("prog");
}
```

## 🔧 Configuration Options

The search engine can be customized by modifying:

- The analyzer used (currently `StandardAnalyzer`)
- Fields to search across in `MultiFieldQueryParser`
- Boost factors for fields (giving some fields higher priority)
- Number of suggestions returned by autocomplete
