using System;

namespace ChieBot.DYK
{
    [Serializable]
    public class DidYouKnowException : Exception
    {
        public DidYouKnowException() { }
        public DidYouKnowException(string message) : base(message) { }
        public DidYouKnowException(string message, Exception inner) : base(message, inner) { }
    }
}
