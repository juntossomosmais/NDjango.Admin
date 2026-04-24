using System;

namespace NDjango.Admin
{
    public class NDjangoAdminManagerException : Exception
    {
        public NDjangoAdminManagerException(string message) : base(message)
        { }

        /// <summary>Initializes a new instance with the specified error message and inner exception.</summary>
        public NDjangoAdminManagerException(string message, Exception innerException) : base(message, innerException)
        { }
    }

    public class RecordNotFoundException : NDjangoAdminManagerException
    {
        public RecordNotFoundException(string sourceId, string recordKey)
            : base($"Can't found the record with ID {recordKey} in {sourceId}")
        { }
    }

    public class ContainerNotFoundException : NDjangoAdminManagerException
    {
        public ContainerNotFoundException(string sourceId) : base($"Container is not found: {sourceId}")
        { }
    }

    /// <summary>
    /// Thrown when a write operation fails a database constraint (unique, foreign key, NOT NULL, length, range).
    /// The message is translated from the provider-specific error when recognized.
    /// </summary>
    public class DataIntegrityException : NDjangoAdminManagerException
    {
        /// <summary>Initializes a new instance with the specified error message.</summary>
        public DataIntegrityException(string message) : base(message)
        { }

        /// <summary>Initializes a new instance with the specified error message and inner exception.</summary>
        public DataIntegrityException(string message, Exception innerException) : base(message, innerException)
        { }
    }

    /// <summary>
    /// Thrown when a record key supplied via URL or form cannot be converted to the CLR type of the key property.
    /// </summary>
    public class InvalidRecordKeyException : NDjangoAdminManagerException
    {
        /// <summary>Initializes a new instance with the specified error message.</summary>
        public InvalidRecordKeyException(string message) : base(message)
        { }
    }
}
