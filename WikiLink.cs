namespace ChieBot
{
    class WikiLink
    {
        public string Link { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return "[[" + Link + (Text == null ? "" : "|" + Text) + "]]";
        }
    }
}
