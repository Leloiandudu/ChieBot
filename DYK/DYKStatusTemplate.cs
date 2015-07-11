using System;
using System.Globalization;
using System.Linq;

namespace ChieBot.DYK
{
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
}
