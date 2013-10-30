﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ChieBot.DYK
{
    public class DidYouKnow
    {
        private const string TemplateName = "Шаблон:Знаете ли вы";
        private const string TemplateTalkName = "Обсуждение шаблона:Знаете ли вы";
        private const string TemplateTalkArchiveName = "Обсуждение шаблона:Знаете ли вы/Архив/{0}";
        private const string ArchiveName = "Проект:Знаете ли вы/Архив рубрики/{0:yyyy-MM}";
        private const string DraftName = "Проект:Знаете ли вы/Черновик";
        private const string DraftTalkName = "Обсуждение проекта:Знаете ли вы/Черновик";
        private const string DraftTalkArchiveName = "Обсуждение проекта:Знаете ли вы/Черновик/Архив/{0}";
        private const string NextIssueName = "Проект:Знаете ли вы/Подготовка следующего выпуска";
        private const string NextIssueNameHeader = "Проект:Знаете ли вы/Подготовка следующего выпуска/Шапка";

        private readonly MediaWiki _wiki;

        public DidYouKnow(MediaWiki wiki)
        {
            _wiki = wiki;
        }

        public string GetCurrent()
        {
            return new Template(_wiki.GetPage(TemplateName)).IssueText;
        }

        public void SetCurrent(string text)
        {
            var template = new Template(_wiki.GetPage(TemplateName));
            template.IssueText = text;
            _wiki.Edit(TemplateName, template.FullText, "Автоматическая публикация выпуска.");
        }

        public void ArchiveCurrent(DateTime issueDate, DateTime archiveDate)
        {
            var titleFormat = (issueDate.Month != archiveDate.Month)
                ? "{0:d MMMM} — {1:d MMMM}"
                : "{0:%d} — {1:d MMMM}";

            _wiki.Edit(
                GetArchiveName(archiveDate),
                string.Format("== {0} ==\n\n{1}\n\n",
                    string.Format(Utils.DateTimeFormat, titleFormat, issueDate, archiveDate),
                    GetCurrent()
                ),
                "Автоматическая архивация прошлого выпуска.",
                false
            );
        }

        public bool ArchiveCurrentTalk(DateTime issueDate)
        {
            var draft = PopDraft(issueDate, TemplateTalkName, false);
            if (draft == null) return false;

            _wiki.Edit(
                GetTalkArchiveName(issueDate),
                draft.FullText,
                "Автоматическая архивация обсуждения прошлого выпуска.",
                true
            );
            return true;
        }

        public string PopDraft(DateTime issueDate)
        {
            return PopDraft(issueDate, DraftName, true).GetIssueText();
        }

        private Draft PopDraft(DateTime issueDate, string pageName, bool required)
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
            _wiki.Edit(pageName, drafts.FullText, "Автоматическая публикация выпуска.");

            return draft;
        }

        public bool ArchiveDraftTalk(DateTime issueDate)
        {
            var draft = PopDraft(issueDate, DraftTalkName, false);
            if (draft == null) return false;

            _wiki.Edit(
                GetDraftTalkArchiveName(issueDate),
                "\n\n" + draft.FullText,
                "Автоматическая архивация обсуждения прошлого выпуска.",
                true
            );
            return true;
        }

        public void RemoveMarkedFromNextIssue(DateTime issueDate)
        {
            var nip = new NextIssuePreparation(_wiki.GetPage(NextIssueName));
            foreach (var item in nip.Where(x => x.IssueDate == issueDate).ToList())
                nip.Remove(item);
            _wiki.Edit(NextIssueName, nip.Text, "Автоматическое удаление использованных анонсов.");
        }

        public void RemoveFromPreparationTimetable(DateTime issueDate)
        {
            var niph = new NextIssuePreparationHeader(_wiki.GetPage(NextIssueNameHeader));
            var item = niph.SingleOrDefault(x => x.Date == issueDate);
            if (item == null)
                throw new DidYouKnowException("Не удалось найти анонс в расписании обновлений.");
            niph.Remove(item);
            _wiki.Edit(NextIssueNameHeader, niph.Text, "Автоматическое удаление использованных анонсов.");
        }

        private string GetArchiveName(DateTime date)
        {
            return string.Format(ArchiveName, date);
        }

        private string GetTalkArchiveName(DateTime date)
        {
            return string.Format(TemplateTalkArchiveName, date.Year - 2008);
        }

        private string GetDraftTalkArchiveName(DateTime date)
        {
            return string.Format(DraftTalkArchiveName, date.Year - 2011);
        }
    }
}
