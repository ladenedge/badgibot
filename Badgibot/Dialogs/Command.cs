using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace Badgibot
{
    class Command
    {
        public Command()
        {
            HasDateFilter = HasType = HasId = false;

            StartTime = new DateTime(2014, 1, 1);
            EndTime = new DateTime(DateTime.Now.Year, 12, 31, 23, 59, 59);

            ContextLines = 2;
        }

        public string Author { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ContextLines { get; set; }
        public int Id { get; set; }
        public MessageType Type { get; set; }

        public bool HasId { get; set; }
        public bool HasDateFilter { get; set; }
        public bool HasType { get; set; }

        public static Command Parse(string raw)
        {
            var cmd = new Command();
            var tokens = raw.Split();

            foreach (var token in tokens)
                foreach (var test in TokenTests)
                    if (test(cmd, token))
                        continue;

            return cmd;
        }

        static IEnumerable<Func<Command, string, bool>> TokenTests = new Func<Command, string, bool>[]
        {
            (cmd, t) => cmd.TrySetAuthor(t),
            (cmd, t) => cmd.TrySetDate(t),
            //(cmd, t) => cmd.TrySetContextLines(t),
            (cmd, t) => cmd.TrySetId(t),
            (cmd, t) => cmd.TrySetType(t),
        };

        private bool TrySetAuthor(string token)
        {
            if (token.StartsWith("uni", StringComparison.OrdinalIgnoreCase))
            {
                Author = "Unicorn";
                return true;
            }
            else if (token.StartsWith("bad", StringComparison.OrdinalIgnoreCase))
            {
                Author = "Badger";
                return true;
            }
            return false;
        }

        private bool TrySetDate(string token)
        {
            DateTime d;
            if (DateTime.TryParseExact(token, "yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out d))
            {
                StartTime = new DateTime(d.Year, StartTime.Month, 1);
                EndTime = new DateTime(d.Year, EndTime.Month, DateTime.DaysInMonth(d.Year, EndTime.Month));
                HasDateFilter = true;
            }
            if (DateTime.TryParseExact(token, "MMM", CultureInfo.CurrentCulture, DateTimeStyles.None, out d))
            {
                StartTime = new DateTime(StartTime.Year, d.Month, 1);
                EndTime = new DateTime(StartTime.Year, d.Month, DateTime.DaysInMonth(StartTime.Year, d.Month));
                HasDateFilter = true;
            }
            return HasDateFilter;
        }

        private bool TrySetId(string token)
        {
            int id;
            if (!Int32.TryParse(token, out id))
                return false;
            if (id < 150000)
                return false;
            HasId = true;
            Id = id;
            return true;
        }

        private bool TrySetContextLines(string token)
        {
            int lines;
            if (!Int32.TryParse(token, out lines))
                return false;
            if (lines < 1 || lines > 10)
                return false;
            ContextLines = lines;
            return true;
        }

        private bool TrySetType(string token)
        {
            foreach (var msgType in Enum.GetNames(typeof(MessageType)))
            {
                if (String.Equals(token, msgType, StringComparison.OrdinalIgnoreCase))
                {
                    Type = (MessageType)Enum.Parse(typeof(MessageType), msgType);
                    HasType = true;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            var args = new[]
            {
                Author,
                ContextLines + " lines",
                HasType ? Type.ToString() : "",
                HasDateFilter ? (StartTime.ToShortDateString() + "->" + EndTime.ToShortDateString()) : "",
            };
            return String.Join("|", args.Where(arg => !String.IsNullOrEmpty(arg)));
        }
    }
}
