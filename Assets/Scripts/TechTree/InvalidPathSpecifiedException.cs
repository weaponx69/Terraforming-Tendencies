using System;

namespace GameDevTV.RTS.TechTree
{
    public class InvalidPathSpecifiedException : Exception
    {
        public InvalidPathSpecifiedException(string attributeName) 
            : base($"{attributeName} does not exist at the provided path!") {}
    }
}