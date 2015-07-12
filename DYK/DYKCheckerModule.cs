using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class DYKCheckerModule : Modules.IModule
    {
        private const int MinArticleSize = 4 * 1024;

        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            wiki.Login(credentials.Login, credentials.Password);

            var preparation = new NextIssuePreparation(wiki.GetPage(DidYouKnow.NextIssueName));
            if (CheckPreparation(preparation, wiki, commandLine.Contains("-onlyNew")))
                wiki.Edit(DidYouKnow.NextIssueName, preparation.FullText, "Автоматическое обновление страницы.");
        }

        private bool CheckPreparation(NextIssuePreparation preparation, MediaWiki wiki, bool onlyNew)
        {
            var hasChanges = false;
            var newIndex = 0;
            foreach (var section in preparation.Sections.ToArray())
            {
                if (section.Articles.All(a => a.Status == null))
                {
                    preparation.Sections.Remove(section);
                    preparation.Sections.Insert(newIndex++, section);
                    hasChanges = true;
                }

                foreach (var article in section.Articles)
                {
                    if (onlyNew && article.Status != null)
                        continue;
                    if (article.Status != null && article.Status.Extra != null)
                        continue;

                    article.Status = CheckStatus(wiki, article.Title) ?? CheckValidness(wiki, article.Title);
                    hasChanges = true;
                }
            }

            if (hasChanges)
                preparation.Update();

            return hasChanges;
        }

        private static readonly Regex ForDeletionRegex = CreateTemlateRegex("к удалению");
        private static readonly Regex NominatedRegex = CreateTemlateRegex("Кандидат в хорошие статьи", "Кандидат в избранные статьи");

        private DYKStatusTemplate CheckStatus(MediaWiki wiki, string title)
        {
            var text = wiki.GetPage(title);

            if (text == null)
                return DYKStatusTemplate.Missing();
            else if (ForDeletionRegex.IsMatch(text))
                return DYKStatusTemplate.ForDeletion();
            else if (NominatedRegex.IsMatch(text))
                return DYKStatusTemplate.Nominated();
            else if (Encoding.UTF8.GetByteCount(text) < MinArticleSize)
                return DYKStatusTemplate.Small();
            else
                return null;
        }

        private DYKStatusTemplate CheckValidness(MediaWiki wiki, string title)
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

        private static DateTimeOffset? GetValidThroughTime(MediaWiki wiki, string title, DateTimeOffset date)
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
            var parent = parentId == 0
                ? new MediaWiki.RevisionInfo()
                : wiki.GetRevisionInfo(parentId);
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
