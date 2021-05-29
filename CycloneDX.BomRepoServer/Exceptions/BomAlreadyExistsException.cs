using System;

namespace CycloneDX.BomRepoServer.Exceptions
{
    public class BomAlreadyExistsException : Exception
    {
        public BomAlreadyExistsException() : base() {}
    }
}