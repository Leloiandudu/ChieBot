﻿using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class DYKCheckerModule : Modules.IModule
    {
        private const int MinArticleSize = 4 * 1024;

        public void Execute(IMediaWiki wiki, string[] commandLine)
        {
            var retries = 5;
            for (var i = 0; i < retries; i++)
            {
                try
                {
                    var page = wiki.GetLastRevision(DidYouKnow.NextIssueName);
                    var preparation = new NextIssuePreparation(page.Text);
                    if (CheckPreparation(preparation, wiki, commandLine.Contains("-onlyNew")))
                        wiki.Edit(DidYouKnow.NextIssueName, preparation.FullText, "Автоматическое обновление страницы.", revId: page.Id);
                    return;
                }
                catch (MediaWikiApiException ex)
                {
                    if (ex.Code == MediaWiki.ErrorCode.EditConflict)
                        continue;
                    throw;
                }
            }

            throw new Exception($"Edit conflict after {retries} retries");
        }

        private bool CheckPreparation(NextIssuePreparation preparation, IMediaWiki wiki, bool onlyNew)
        {
            var hasChanges = false;

            if (preparation.NewSections != null && preparation.NewSections.Count > 0)
            {
                preparation.Sections.InsertRange(0, preparation.NewSections);
                preparation.NewSections.Clear();
                hasChanges = true;
            }

            foreach (var article in preparation.Sections.SelectMany(s => s.Articles).ToArray())
            {
                if (onlyNew && article.Status != null)
                    continue;
                if (article.Status != null && article.Status.Extra != null)
                    continue;

                article.Status = CheckStatus(wiki, article.Title) ?? CheckValidness(wiki, article.Title);
                hasChanges = true;
            }

            if (hasChanges)
                preparation.Update();

            return hasChanges;
        }

        private static readonly Regex ForDeletionRegex = CreateTemlateRegex("к удалению");

        private DYKStatusTemplate CheckStatus(IMediaWiki wiki, string title)
        {
            var text = wiki.GetPage(title, followRedirects: true);

            if (text == null)
                return DYKStatusTemplate.Missing();
            else if (ForDeletionRegex.IsMatch(text))
                return DYKStatusTemplate.ForDeletion();
            else if (Encoding.UTF8.GetByteCount(text) < MinArticleSize)
                return DYKStatusTemplate.Small();
            else
                return null;
        }

        private DYKStatusTemplate CheckValidness(IMediaWiki wiki, string title)
        {
            var validThrough = GetValidThroughTime(wiki, title, DateTimeOffset.UtcNow);
            return validThrough == null
                ? DYKStatusTemplate.Missing()
                : DYKStatusTemplate.Valid(validThrough.Value);
        }

        private static Regex CreateTemlateRegex(params string[] templateNames)
        {
            var names = string.Join("|", templateNames.Select(Regex.Escape));
            return new Regex(@"\{\{\s*(" + names + @")\s*(\||\}\})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);
        }

        private static DateTimeOffset? GetValidThroughTime(IMediaWiki wiki, string title, DateTimeOffset date)
        {
            var oldDate = date.AddMonths(-3);
            var history = wiki.GetHistory(title, oldDate);

            if (history == null)
                return null;// article doesn't exist

            if (history.Length == 0)
                return DateTimeOffset.MinValue; // no revisions for 3 months, thus not valid

            // retreiving immediate parent of the first "3 months old" edit
            // or faking the "creation" with a zero-sized revision
            var parentId = history.First().ParentId;
            var parent = parentId == 0 ? null : wiki.GetRevisionInfo(parentId);
            parent = parent ?? new MediaWiki.RevisionInfo();
            parent.Timestamp = oldDate.AddTicks(-1);

            // searching for the first revision that makes article non-valid
            foreach (var entry in new[] { parent }.Concat(history))
            {
                var newDate = entry.Timestamp.AddMonths(3);
                var newSize = GetArticleSize(newDate, history);
                if (entry.Size * 2 > newSize)
                    return newDate == parent.Timestamp.AddMonths(3) ? DateTimeOffset.MinValue : newDate;
            }

            throw new Exception("Impossible...");
        }

        private static int GetArticleSize(DateTimeOffset date, MediaWiki.RevisionInfo[] history)
        {
            return (history.LastOrDefault(h => h.Timestamp <= date) ?? history.Last()).Size;
        }

        class Announce : Section
        {
            public DateTimeOffset Date { get; set; }
        }

        class Announces : SectionedArticle<Announce>
        {
            private static readonly Regex DateRegex = new Regex(@"^== ?\d+( \w+)?(\s*[—\-]\s*(?<day>\d+))? (?<month>\w+)", RegexOptions.ExplicitCapture);
            private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

            private static int _year;

            public Announces(string fullText, int year)
                : base(Init(fullText, year))
            {
            }

            private static string Init(string fullText, int year)
            {
                _year = year;
                return fullText;
            }

            protected override bool InitSection(Announce section)
            {
                var match = DateRegex.Match(section.Title);
                if (!match.Success)
                    return false;

                var date = string.Format("{0} {1} {2}", match.Groups["day"].Value, match.Groups["month"].Value, _year);
                section.Date = DateTime.ParseExact(date, "d MMMM yyyy", RuCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).AddDays(-3);
                return true;
            }
        }
    }
}
