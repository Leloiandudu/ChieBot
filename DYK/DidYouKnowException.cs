using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ChieBot.DYK
{
    [Serializable]
    public class DidYouKnowException : Exception
    {
        public DidYouKnowException() { }
        public DidYouKnowException(string message) : base(message) { }
        public DidYouKnowException(string message, Exception inner) : base(message, inner) { }
        protected DidYouKnowException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
