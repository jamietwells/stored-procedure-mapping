using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StoredProcedureMapping
{
    public class StoredProcedureMapper
    {
        private readonly FolderSearcher _searcher;

        public StoredProcedureMapper(string path, Action<string> log)
        {
            _searcher = new FolderSearcher(path, log);
        }

        public StoredProcedureMapper(string path) : this(path, Console.WriteLine) { }

        public void Map(string schema, string startName)
        {
            _searcher
                .Search(startName, schema)
                .Print(_searcher);
        }
    }

    public static class TrimSquareBracketsExtension
    {
        public static string TrimSquareBrackets(this string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return value.TrimStart('[').TrimEnd(']');
        }
    }

    public class StoredProcedureInfo
    {
        public StoredProcedureInfo(FileInfo file, Action<string> log)
        {
            _fileName = file.Name;
            _text = File.ReadAllText(file.FullName);
            var match = _namePattern.Match(_text);
            _schema = match.Groups.ElementAt(1).Success ? match.Groups.ElementAt(1).Value : "dbo";
            _name = match.Groups.ElementAt(2).Value;

            if (string.IsNullOrWhiteSpace(_name))
                throw new ArgumentException("Cannot be an empty string", nameof(_name));

            if (string.IsNullOrWhiteSpace(_schema))
                throw new ArgumentException("Cannot be an empty string", nameof(_schema));

            _log = log;
            //_log($"Reading {ConstructSPName()}");
        }

        private readonly string _fileName;
        private readonly string _text;
        private readonly string _schema;
        private readonly string _name;

        private static readonly Regex _namePattern = new Regex(@"CREATE\s+PROC(?:EDURE)?\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?", options: RegexOptions.IgnoreCase);
        private static readonly Regex _innerExecPattern = new Regex(@"(?<!\s*--.*)(?<!\s*print\s*\')EXEC(?:UTE)?(?!\s*ON)\s+(?:@\w+\s+=\s+)?(?:\[?([\w-]+)\]?\.)?(?:(?:\[?([\w-]+)\]?\.)|\.)?(?:\[?([\w-]+)\]?)", options: RegexOptions.IgnoreCase);

        public bool IsMatch(string procName, string schemaName) =>
            _name.Equals(procName, StringComparison.OrdinalIgnoreCase) &&
            _schema.Equals(schemaName, StringComparison.OrdinalIgnoreCase);

        private readonly Action<string> _log;

        private string Padding(int level) => string.Concat(Enumerable.Range(0, level).Select(i => "|\t"));

        private string ConstructSPName() => $"[{_schema.TrimSquareBrackets()}].[{_name.TrimSquareBrackets()}]";

        private IEnumerable<Match> EnumerateInnerMatches()
        {
            var result = _innerExecPattern.Match(_text);
            if (result.Success)
                yield return result;

            bool NextMatch()
            {
                result = result.NextMatch();
                if (result == null)
                    if (result.Success)
                        throw new Exception("We made an invalid assumption");
                return result.Success;
            }
            while (NextMatch())
            {
                yield return result;
            }
        }

        private IEnumerable<NameSchemaPair> EnumerateInnerProcedures() =>
            EnumerateInnerMatches()
                .Select(MatchToNameSchema);

        private NameSchemaPair MatchToNameSchema(Match match)
        {
            var name = match.Groups[3].Value;
            var schema = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Success ? match.Groups[1].Value : "dbo";

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Cannot be an empty string", nameof(name));

            if (string.IsNullOrWhiteSpace(schema))
                throw new ArgumentException("Cannot be an empty string", nameof(schema));

            return new NameSchemaPair(name, schema);
        }

        private IEnumerable<StoredProcedureInfo> FindInner(FolderSearcher searcher) =>
            EnumerateInnerProcedures()
                .Select(d => searcher.Search(d.Name, d.Schema))
                .Where(a => a != null);

        public void Print(FolderSearcher searcher) => Print(searcher, new List<string>(), 0);
        private void Print(FolderSearcher searcher, IEnumerable<string> seenProcs, int level)
        {
            _log($"{Padding(level)}{ConstructSPName()}");
            foreach (var innderProc in FindInner(searcher))
            {
                if (seenProcs.Contains(innderProc.ConstructSPName()))
                    _log($"{Padding(level + 1)}{innderProc.ConstructSPName()}...");
                else
                    innderProc.Print(searcher, seenProcs.Concat(new[] { innderProc.ConstructSPName() }), level + 1);
            }
        }

        private class NameSchemaPair
        {
            public NameSchemaPair(string name, string schema)
            {
                Name = name;
                Schema = schema;
            }
            public string Name { get; }
            public string Schema { get; }
        }

    }

    public class FolderSearcher
    {
        private string _path;
        private readonly Action<string> _log;
        private List<StoredProcedureInfo> _files;

        public FolderSearcher(string path, Action<string> log)
        {
            _path = path;
            _log = log;
            _files = Directory.EnumerateFiles(path)
                .Select(s => new FileInfo(s))
                .Where(f => f.Extension.Equals(".sql", comparisonType: StringComparison.OrdinalIgnoreCase))
                .Where(f => f.DirectoryName.EndsWith("Procedures"))
                .Select(ReadInfo)
                .ToList();
        }

        private StoredProcedureInfo ReadInfo(FileInfo file) =>
            new StoredProcedureInfo(file, _log);

        public StoredProcedureInfo Search(string procName, string schemaName) =>
            _files
                .Where(f => f.IsMatch(procName, schemaName))
                .SingleOrDefault();
    }
}