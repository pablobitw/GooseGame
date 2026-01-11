using System.Data.SqlClient;
using System.Reflection;
using System;

namespace GameServer.Tests
{
    public class SqlExceptionFactory
    {
        public SqlException CreateSqlException()
        {
            var errorCollectionType = typeof(SqlErrorCollection);

            var errorCollectionConstructor = errorCollectionType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes, 
                null);

            if (errorCollectionConstructor == null)
            {
                throw new InvalidOperationException("No se pudo encontrar el constructor de SqlErrorCollection.");
            }

            var errorCollection = errorCollectionConstructor.Invoke(null);

            var exceptionType = typeof(SqlException);

            var createMethod = exceptionType.GetMethod(
                "CreateException",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { errorCollectionType, typeof(string) },
                null);

            if (createMethod == null)
            {
                throw new InvalidOperationException("No se pudo encontrar el método CreateException en SqlException.");
            }

            var exception = (SqlException)createMethod.Invoke(null, new object?[] { errorCollection, "Simulated Server Down" })!;

            return exception;
        }
    }
}