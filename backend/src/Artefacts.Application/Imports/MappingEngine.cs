namespace Artefacts.Application.Imports;

/// <summary>
/// Applies a mapping to the cells of one record row. Every non-empty line of a mapped column ends up in exactly one
/// field: the first line rule that accepts it, the field of a continuing rule above it, or the column's rest field.
/// Lines are only dropped when a column has line rules but no rest field (reported in <see cref="MappedRecord.Dropped"/>),
/// or when a discarding rule matches them (reported in <see cref="MappedRecord.Discarded"/>).
/// </summary>
public static class MappingEngine
{
    /// <summary>
    /// The workbook column each rule applies to: the column with the rule's heading, else the column at the rule's
    /// index, unless another rule claims that column by its heading (a template from a workbook with a "Valore"
    /// column must not take over another workbook's eighth column when it is "William"). Unused rules are left out.
    /// </summary>
    public static IReadOnlyList<(int Column, ColumnRule Rule)> Resolve(ImportMapping mapping, IReadOnlyDictionary<int, string> headings)
    {
        var byHeading = headings
            .GroupBy(h => Key(h.Value))
            .ToDictionary(g => g.Key, g => g.Min(h => h.Key));
        int? Matched(ColumnRule rule) =>
            rule.Heading is { } heading && byHeading.TryGetValue(Key(heading), out var column) ? column : null;
        var claimed = mapping.Columns.Select(Matched).Where(c => c is not null).Select(c => c!.Value).ToHashSet();
        return mapping.Columns
            .Select(rule => (Column: Matched(rule) ?? (claimed.Contains(rule.Column) ? (int?)null : rule.Column), Rule: rule))
            .Where(r => r.Column is not null)
            .Select(r => (r.Column!.Value, r.Rule))
            .ToList();
    }

    private static string Key(string heading) => SheetLayoutReader.Clean(heading).ToLowerInvariant();

    public static MappedRecord Map(IReadOnlyDictionary<int, string> cells, ImportMapping mapping, IReadOnlyDictionary<int, string> headings,
        DetectorRegistry? detectors = null, FieldRegistry? fields = null, LookupTables? tables = null)
    {
        detectors ??= DetectorRegistry.BuiltIn;
        fields ??= FieldRegistry.BuiltIn;
        var values = new List<MappedValue>();
        var dropped = new List<string>();
        var discarded = new List<string>();
        foreach (var (column, rule) in Resolve(mapping, headings))
        {
            if (rule.Ignored || !cells.TryGetValue(column, out var text) || string.IsNullOrWhiteSpace(text)) continue;
            if (rule.UsesLines)
                new LineAssigner(column, rule, mapping, detectors, fields, tables, values, dropped, discarded, headings.GetValueOrDefault(column)).Assign(text);
            else
                MapWholeCell(column, fields.Get(rule.Target!.Value), text, detectors, DetectionContext.For(mapping, tables), values);
        }

        return new MappedRecord(values, dropped, discarded);
    }

    /// <summary>The non-empty lines of a cell with their paragraph number (blank lines separate paragraphs).</summary>
    public static List<(string Text, int Paragraph)> Lines(string cell)
    {
        var result = new List<(string, int)>();
        var paragraph = 0;
        var blank = false;
        foreach (var raw in cell.Split('\n'))
        {
            var line = raw.Trim();
            // Lines of punctuation only carry no text.
            if (Detectors.Tidy(line).Length == 0)
            {
                blank = true;
                continue;
            }

            if (blank && result.Count > 0) paragraph++;
            blank = false;
            result.Add((System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "), paragraph));
        }

        return result;
    }

    private static void MapWholeCell(int column, FieldDefinition field, string text, DetectorRegistry detectors, DetectionContext context, List<MappedValue> values)
    {
        var lines = Lines(text);
        if (lines.Count == 0) return;

        if (field.Shape == CellShape.Paragraphs)
        {
            foreach (var paragraph in lines.GroupBy(l => l.Paragraph))
            {
                var entry = string.Join(", ", paragraph.Select(l => Detectors.Tidy(l.Text)));
                values.Add(new MappedValue(field.Id, entry, column, lines.IndexOf(paragraph.First())));
            }

            return;
        }

        var joined = field.Shape == CellShape.Text
            ? string.Join("\n", text.Split('\n').Select(l => l.TrimEnd())).Trim()
            : string.Join(" ", lines.Select(l => l.Text));
        var detection = field.Parse?.Invoke(joined, detectors, context);
        values.Add(new MappedValue(field.Id, joined, column, 0, detection?.Parsed, detection?.Guessed ?? false));
    }

    private sealed class LineAssigner(
        int column, ColumnRule rule, ImportMapping mapping, DetectorRegistry detectors, FieldRegistry fields, LookupTables? tables, List<MappedValue> values, List<string> dropped, List<string> discarded,
        string? heading)
    {
        private readonly int _firstValue = values.Count;
        private readonly DetectionContext context = DetectionContext.For(mapping, tables);

        /// <summary>True when some line of the cell is a date the rules understand.</summary>
        private bool _cellHasDate;

        public void Assign(string cell)
        {
            var lines = Lines(cell);
            _cellHasDate = lines.Any(l => Domain.Dating.HistoricalDateParser.TryParse(l.Text) is not null);
            ImportField? continuing = null;
            var previousParagraph = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                var (text, paragraph) = lines[i];
                if (paragraph != previousParagraph) continuing = null;
                previousParagraph = paragraph;

                // A date written over two lines ("seconda metà" / "XVI"), or followed by a bracketed line.
                var next = i + 1 < lines.Count && lines[i + 1].Paragraph == paragraph ? lines[i + 1].Text : null;
                var (matched, consumedNext) = TryRules(text, i, next);
                if (consumedNext) i++;
                if (matched is not null)
                {
                    continuing = matched.Value.Continues ? matched.Value.Target : null;
                    continue;
                }

                if (continuing is { } field)
                {
                    Add(field, text, i);
                    continue;
                }

                ToRest(text, i);
            }
        }

        /// <summary>Tries the rules on a line; returns the rule that took it (null when none did).</summary>
        private ((ImportField? Target, bool Continues)? Rule, bool ConsumedNext) TryRules(string text, int position, string? next, int depth = 0)
        {
            foreach (var lineRule in rule.Lines!)
            {
                if (lineRule.Target is { } target && Filled(target)) continue;

                if (lineRule.Detector is { } detector)
                {
                    // One item number per row: a later numbered line ("2 Khorassan sezione") is text of its own.
                    if (detector == DetectorIds.ItemTitle && Filled(ImportField.ItemNumber)) continue;
                    var consumedNext = false;
                    var detection = detectors.Detect(detector, text, context);
                    if (detection is null) continue;
                    // A line that only looks like a date gives way to a real date elsewhere in the cell.
                    if (detection.Parsed is ParsedDateText { Interpretation: null } && _cellHasDate && !JoinsWithNext(detection, next)) continue;

                    if (detector == DetectorIds.HistoricalDate && next is not null && JoinsDate(detection, next, out var joined))
                    {
                        detection = joined;
                        consumedNext = true;
                    }

                    if (lineRule.Target is { } field)
                    {
                        Add(field, detection.Text, position, detection.Parsed, detection.Guessed);
                        if (detection.ItemNumber is { } item && !Filled(ImportField.ItemNumber))
                            Add(ImportField.ItemNumber, item, position);
                    }
                    else
                    {
                        Discard(detection.Text);
                    }
                    if (detection.Remainder is { } remainder && depth < 4)
                    {
                        var (rest, _) = TryRules(remainder, position, null, depth + 1);
                        if (rest is null) ToRest(remainder, position);
                        else return (rest, consumedNext);
                    }

                    return ((lineRule.Target, lineRule.Continues && !lineRule.Discards), consumedNext);
                }

                if (lineRule.Position == position)
                {
                    if (lineRule.Target is { } field) Add(field, text, position);
                    else Discard(text);
                    return ((lineRule.Target, lineRule.Continues && !lineRule.Discards), false);
                }
            }

            return (null, false);
        }

        /// <summary>"seconda metà" followed by "XVI": the line is the start of the cell's real date.</summary>
        private static bool JoinsWithNext(Detection detection, string? next) =>
            next is not null && Domain.Dating.HistoricalDateParser.TryParse(detection.Text + " " + next) is not null;

        private static bool JoinsDate(Detection detection, string next, out Detection joined)
        {
            joined = detection;
            var current = (ParsedDateText)detection.Parsed!;
            var text = detection.Text + " " + next;
            var parsed = Domain.Dating.HistoricalDateParser.TryParse(text);
            if (next.StartsWith('(') || (current.Interpretation is null && parsed is not null))
            {
                joined = new Detection(text, new ParsedDateText(text, parsed));
                return true;
            }

            return false;
        }

        private bool Filled(ImportField field) =>
            fields.IsSingleValued(field) && values.Skip(_firstValue).Any(v => v.Field == field);

        private void Add(ImportField field, string text, int line, object? parsed = null, bool guessed = false) =>
            values.Add(new MappedValue(field, text, column, line, parsed, guessed));

        private void Discard(string text) => discarded.Add($"{heading ?? $"Column {column}"}: \"{text}\"");

        private void ToRest(string text, int line)
        {
            if (rule.Rest is { } rest) Add(rest, text, line);
            else dropped.Add($"{heading ?? $"Column {column}"}, line {line + 1}: \"{text}\"");
        }
    }
}
