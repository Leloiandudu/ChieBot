using System;
using System.Collections.Generic;
using System.Linq;
using static ChieBot.DYK.NextIssuePreparation;

namespace ChieBot.DYK
{
    public class DidYouKnow
    {
        public const string TemplateName = "Шаблон:Знаете ли вы";
        private const string ArchiveName = "Проект:Знаете ли вы/Архив рубрики/{0:yyyy-MM}";
        public const string DraftName = "Проект:Знаете ли вы/Черновик";
        public const string DraftTalkName = "Обсуждение проекта:Знаете ли вы/Черновик";
        private const string DraftTalkArchiveName = "Обсуждение проекта:Знаете ли вы/Черновик/Архив/{0}";
        public const string NextIssueName = "Проект:Знаете ли вы/Подготовка следующего выпуска";
        public const string NextIssueNameHeader = "Проект:Знаете ли вы/Расписание";

        /// <summary>Period between DYK (in days).</summary>
        public const int PeriodInDays = 3;

        private readonly IMediaWiki _wiki;
        private readonly DateTimeOffset _prevIssueDate;
        private readonly DateTimeOffset _nextIssueDate;

        public DidYouKnow(IMediaWiki wiki, DateTimeOffset nextIssueDate)
        {
            _wiki = wiki;
            _prevIssueDate = nextIssueDate.AddDays(-PeriodInDays);
            _nextIssueDate = nextIssueDate;
        }

        public string GetCurrent()
        {
            return new DykTemplate(_wiki.GetPage(TemplateName)).IssueText;
        }

        public void SetCurrent(string text)
        {
            var template = new DykTemplate(_wiki.GetPage(TemplateName)) { IssueText = text };
            _wiki.Edit(TemplateName, template.FullText, "Автоматическая публикация выпуска.");
        }

        public string GetArchiveTitle()
        {
            var titleFormat = _prevIssueDate.Month != _nextIssueDate.Month
                ? "{0:d MMMM} — {1:d MMMM}"
                : "{0:%d}—{1:d MMMM}";

            return string.Format(Utils.DateTimeFormat, titleFormat, _prevIssueDate, _nextIssueDate);
        }

        public void ArchiveCurrent()
        {
            var current = GetCurrent();
            _wiki.Edit(
                GetArchiveName(),
                $"== {GetArchiveTitle()} ==\n\n{current}\n\n",
                "Автоматическая архивация прошлого выпуска.",
                false
            );

            PostHooks(current);
        }

        public void PostHooks(string issue)
        {
            var datesFormat =
                _prevIssueDate.Year != _nextIssueDate.Year ? "{0:d MMMM yyyy} — {1:d MMMM yyyy}" :
                _prevIssueDate.Month != _nextIssueDate.Month ? "{0:d MMMM} — {1:d MMMM yyyy}" :
                "{0:%d}—{1:d MMMM yyyy}";

            var dates = string.Format(Utils.DateTimeFormat, datesFormat, _prevIssueDate, _nextIssueDate);

            var archive = $"{_nextIssueDate:yyyy-MM}#{GetArchiveTitle()}";
            var parser = new IssueParser();

            foreach (var (title, text, image) in parser.Parse(issue))
            {
                var talkTitle = $"Talk:{title}";

                var page = _wiki.GetPage(talkTitle);
                if (page != null && page.Contains("{{Сообщение ЗЛВ"))
                    continue;

                var template = new Template
                {
                    Name = "Сообщение ЗЛВ",
                    Args =
                    {
                        { "даты", dates },
                        { "текст", text.TrimEnd() },
                        { "архив", archive },
                    },
                };

                if (image != null)
                    template.Args.Add("иллюстрация2", image);

                _wiki.Edit(talkTitle, template.ToString(), "Простановка сообщения проекта «[[Проект:Знаете ли вы|Знаете ли вы]]»", false);
            }

            if (parser.Errors == null)
                return;

            var errors = $"=== Ошибки архивации ===\n{parser.Errors}\n\nПожалуйста исправьте их врунчную ~~~~";
            var drafts = new Drafts(_wiki.GetPage(DraftTalkName));
            var draft = drafts[_nextIssueDate.ToDateOnly()];

            if (draft == null)
            {
                draft = new Draft
                {
                    Title = $"Выпуск {DYKUtils.FormatIssueDate(_nextIssueDate)}",
                    Text = errors,
                };
                drafts.Add(draft);
            }
            else
            {
                draft.Text = draft.Text.TrimEnd() + "\n\n" + errors;
            }

            _wiki.Edit(DraftTalkName, drafts.FullText, "Автоматическая публикация выпуска.");
        }

        public string PopDraft()
        {
            return PopDraft(_nextIssueDate.ToDateOnly(), DraftName, true, "Автоматическая публикация выпуска.").GetIssueText();
        }

        private Draft PopDraft(DateOnly issueDate, string pageName, bool required, string editSummary)
        {
            var drafts = new Drafts(_wiki.GetPage(pageName));
            var draft = drafts[issueDate];

            if (draft == null)
            {
                if (required)
                    throw new DidYouKnowException(string.Format("Черновик за {0} не найден.", issueDate));
                else
                    return null;
            }

            drafts.Remove(draft);
            _wiki.Edit(pageName, drafts.FullText, editSummary);

            return draft;
        }

        public bool ArchiveDraftTalk()
        {
            DateOnly issueDate = _prevIssueDate.ToDateOnly();

            const string summary = "Автоматическая архивация обсуждения позапрошлого выпуска.";
            var draft = PopDraft(issueDate, DraftTalkName, false, summary);
            if (draft == null) return false;

            _wiki.Edit(
                GetDraftTalkArchiveName(issueDate),
                "\n\n" + draft.FullText,
                summary,
                true
            );
            return true;
        }

        public void RemoveMarkedFromNextIssue()
        {
            var issueDate = _nextIssueDate.ToDateOnly();

            var nip = new NextIssuePreparation(_wiki.GetPage(NextIssueName));
            foreach (var item in nip.Sections.Where(x => x.GetIssueDate() == issueDate).ToList())
                nip.Sections.Remove(item);
            nip.Update();
            _wiki.Edit(NextIssueName, nip.FullText, "Автоматическое удаление использованных анонсов.");
        }

        public void RemoveFromPreparationTimetable()
        {
            var issueDate = _nextIssueDate.ToDateOnly();

            var niph = new NextIssuePreparationHeader(_wiki.GetPage(NextIssueNameHeader));
            var item = niph.SingleOrDefault(x => x.Date == issueDate);
            if (item == null)
                throw new DidYouKnowException("Не удалось найти анонс в расписании обновлений.");
            niph.Remove(item);
            _wiki.Edit(NextIssueNameHeader, niph.Text, "Автоматическое удаление использованных анонсов.");
        }

        private string GetArchiveName() =>
            string.Format(ArchiveName, _nextIssueDate);

        private static string GetDraftTalkArchiveName(DateOnly date) =>
            string.Format(DraftTalkArchiveName, date.Year - 2011);

        public void Stabilize(string draft)
        {
            var until = _nextIssueDate.AddDays(PeriodInDays);

            foreach (var article in ParserUtils.FindBoldLinks(draft))
            {
                if (_wiki.GetStabilizationExpiry(article, out var expiry))
                {
                    if (expiry == null || expiry >= until)
                        continue;
                }

                _wiki.Stabilize(article, "Автоматическая стабилизация: на заглавной до " + until.ToString("dd MMMM", Utils.DateTimeFormat), until);
            }
        }
    }
}
