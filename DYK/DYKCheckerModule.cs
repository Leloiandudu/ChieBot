using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChieBot.DYK
{
    class DYKCheckerModule : Modules.IModule
    {
        public void Execute(MediaWiki wiki, string[] commandLine, Credentials credentials)
        {
            CheckPreparation(wiki, commandLine.Contains("-onlyNew"));
        }

        private void CheckPreparation(MediaWiki wiki, bool onlyNew)
        {
            var preparation = new Preparation(wiki.GetPage(DidYouKnow.NextIssueName));

            var hasChanges = false;
            foreach (var article in preparation.SelectMany(i => i.Articles.ToArray()))
            {
                if (onlyNew && article.Status != null)
                    continue;
                if (article.Status != null && article.Status.Extra != null)
                    continue;
                article.Status = CheckStatus(wiki, article.Title) ?? CheckValidness(wiki, article.Title);
                hasChanges = true;
            }

            if (hasChanges)
            {
                preparation.Update();
                wiki.Edit(DidYouKnow.NextIssueName, preparation.FullText, "Автоматическое обновление страницы.");
            }
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

        class Preparation : SectionedArticle<Preparation.Item>
        {
            private static readonly Regex ArticleRegex = new Regex(@"(\{\{(?<status>" + Regex.Escape(DYKStatusTemplate.TemplateName) + @"\|[^}]+)\}\})?\s*\[\[(?<title>[^\]]+)\]\](,\s*)?", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

            public Preparation(string text)
                : base(text, 3)
            {
            }

            protected override bool InitSection(Item section)
            {
                section.Articles = new PartiallyParsedWikiText<Article>(section.Title, ArticleRegex, m => new Article(m));
                return true;
            }

            public void Update()
            {
                foreach (var item in this)
                    item.Update();
            }

            public class Item : Section
            {
                public PartiallyParsedWikiText<Article> Articles { get; set; }

                public void Update()
                {
                    Title = string.Format("=== {0} ===\r\n", string.Join(", ", Articles));
                }
            }
        }

        class Article
        {
            public Article(Match match)
            {
                var status = match.Groups["status"];
                Title = match.Groups["title"].Value;
                Status = status.Success ? new DYKStatusTemplate(status.Value) : null;
            }

            public string Title { get; private set; }

            public DYKStatusTemplate Status { get; set; }

            public override string ToString()
            {
                var result = string.Format("[[{0}]]", Title);
                if (Status != null)
                    result = Status.ToString() + " " + result;
                return result;
            }
        }

        class DYKStatusTemplate
        {
            public const string TemplateName = "злв-статус";
            private const string ValidThroughArg = "до=";
            private const string MissingArg = "отсутствует";
            private const string ForDeletionArg = "КУ";
            private const string NominatedArg = "номинирована";
            private const string MinDate = "0001-01-01T00:00:00";

            public DateTimeOffset? ValidThrough { get; private set; }
            public string Extra { get; private set; }
            public bool IsMissing { get; private set; }
            public bool IsForDeletion { get; private set; }
            public bool IsNominated { get; private set; }

            private DYKStatusTemplate()
            {
            }

            public static DYKStatusTemplate Valid(DateTimeOffset validThrough, string extra = null)
            {
                return new DYKStatusTemplate
                {
                    ValidThrough = validThrough,
                    Extra = extra,
                };
            }

            public static DYKStatusTemplate Missing(string extra = null)
            {
                return new DYKStatusTemplate
                {
                    IsMissing = true,
                    Extra = extra,
                };
            }

            public static DYKStatusTemplate ForDeletion(string extra = null)
            {
                return new DYKStatusTemplate
                {
                    IsForDeletion = true,
                    Extra = extra,
                };
            }

            public static DYKStatusTemplate Nominated(string extra = null)
            {
                return new DYKStatusTemplate
                {
                    IsNominated = true,
                    Extra = extra,
                };
            }

            public DYKStatusTemplate(string text)
            {
                var args = text.Split(new[] { '|' }, 3).Select(a => a.Trim()).ToArray();

                if (!args[0].Equals(TemplateName, StringComparison.OrdinalIgnoreCase))
                    throw new FormatException(text);

                if (args.Length == 1)
                    return;

                if (args[1].StartsWith(ValidThroughArg, StringComparison.OrdinalIgnoreCase))
                {
                    // for some reason DateTimeOffset.Parse(DateTimeOffset.MinValue.ToString("s")) fails
                    var date = args[1].Substring(ValidThroughArg.Length);
                    ValidThrough = date == MinDate
                        ? DateTimeOffset.MinValue
                        : DateTimeOffset.Parse(date, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                else if (args[1].Equals(MissingArg, StringComparison.OrdinalIgnoreCase))
                {
                    IsMissing = true;
                }
                else if (args[1].Equals(ForDeletionArg, StringComparison.OrdinalIgnoreCase))
                {
                    IsForDeletion = true;
                }
                else if (args[1].Equals(NominatedArg, StringComparison.OrdinalIgnoreCase))
                {
                    IsNominated = true;
                }
                else
                {
                    throw new FormatException("Unknown arg: " + args[1]);
                }

                if (args.Length == 3 && !string.IsNullOrWhiteSpace(args[2]))
                    Extra = args[2];
            }

            public override string ToString()
            {
                var args = string.Join("|", new[]
                {
                    TemplateName,
                    GetFirstArg(),
                    Extra
                }.Where(a => a != null));

                return "{{" + args + "}}";
            }

            private string GetFirstArg()
            {
                if (ValidThrough.HasValue)
                    return ValidThroughArg + ValidThrough.Value.ToUniversalTime().ToString("s");
                else if (IsMissing)
                    return MissingArg;
                else if (IsForDeletion)
                    return ForDeletionArg;
                else if (IsNominated)
                    return NominatedArg;
                else
                    return null;
            }
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
