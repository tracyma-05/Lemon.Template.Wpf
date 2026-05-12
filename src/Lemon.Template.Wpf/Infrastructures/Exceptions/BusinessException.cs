using System;

namespace Lemon.Template.Wpf.Infrastructures.Exceptions
{
    public class BusinessException : Exception
    {
        public BusinessException() { }

        public BusinessException(string message)
            : base(message)
        { }

        public BusinessException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}